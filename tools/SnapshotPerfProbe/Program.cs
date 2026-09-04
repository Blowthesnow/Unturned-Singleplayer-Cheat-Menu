using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static int ReadInt(string[] arguments, string name, int fallback)
{
    int index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length &&
        int.TryParse(arguments[index + 1], out int value) && value > 0
        ? value
        : fallback;
}

static double Percentile(IReadOnlyList<double> values, double percentile)
{
    double[] sorted = values.OrderBy(value => value).ToArray();
    if (sorted.Length == 0) return 0d;
    int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
}

static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

static void FastAtomicSave(SnapshotDocument document, string path, bool verifyPrimary)
{
    string bytesJson = SnapshotDocumentCodec.Serialize(document);
    byte[] bytes = new UTF8Encoding(false).GetBytes(bytesJson);
    string temporary = path + ".fast-" + Guid.NewGuid().ToString("N") + ".tmp";
    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
        FileShare.None, 4096, FileOptions.WriteThrough))
    {
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }
    if (File.Exists(path))
        File.Replace(temporary, path, path + ".bak", ignoreMetadataErrors: true);
    else
        File.Move(temporary, path);
    if (verifyPrimary)
    {
        byte[] expected = SHA256.HashData(bytes);
        byte[] actual = SHA256.HashData(File.ReadAllBytes(path));
        Require(actual.SequenceEqual(expected), "Fast atomic primary verification failed.");
    }
}

int fixtureIndex = Array.IndexOf(args, "--fixture");
Require(fixtureIndex >= 0 && fixtureIndex + 1 < args.Length,
    "Usage: SnapshotPerfProbe --fixture <snapshot.json> [--clone-iterations N] [--serialize-iterations N] [--save-iterations N]");
string fixturePath = Path.GetFullPath(args[fixtureIndex + 1]);
Require(File.Exists(fixturePath), "Fixture does not exist: " + fixturePath);
int cloneIterations = ReadInt(args, "--clone-iterations", 1000);
int serializeIterations = ReadInt(args, "--serialize-iterations", 100);
int saveIterations = ReadInt(args, "--save-iterations", 10);
int probeRootIndex = Array.IndexOf(args, "--probe-root");
string probeBaseRoot = Path.GetFullPath(probeRootIndex >= 0 && probeRootIndex + 1 < args.Length
    ? args[probeRootIndex + 1]
    : Path.GetTempPath());

string json = File.ReadAllText(fixturePath, Encoding.UTF8);
Require(SnapshotDocumentValidator.TryParse(json, out SnapshotDocument document, out string parseError),
    "Fixture is not valid Snapshot JSON: " + parseError);
SnapshotKernel kernel = new SnapshotKernel();
Require(kernel.ReplaceFromDocument(document).Success, "Fixture could not be imported into SnapshotKernel.");
int groups = document.Groups.Count;
int buckets = document.Groups.Sum(group => group.Buckets.Count);
long instances = document.Groups.SelectMany(group => group.Buckets).Sum(bucket => bucket.Count);

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long cloneAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
long cloneChecksum = 0;
Stopwatch cloneWatch = Stopwatch.StartNew();
for (int index = 0; index < cloneIterations; index++)
{
    IReadOnlyList<ItemGroupView> views = kernel.GetGroups();
    cloneChecksum += views.Count + views.Sum(group => group.Buckets.Count);
}
cloneWatch.Stop();
long cloneAllocated = GC.GetAllocatedBytesForCurrentThread() - cloneAllocatedBefore;

SnapshotDocumentGroup firstDocumentGroup = document.Groups.First();
ItemGroupIdentity pointIdentity = new ItemGroupIdentity(firstDocumentGroup.AssetId,
    Guid.Parse(firstDocumentGroup.AssetGuid));
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long pointAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
long pointChecksum = 0;
Stopwatch pointWatch = Stopwatch.StartNew();
for (int index = 0; index < cloneIterations; index++)
{
    Require(kernel.TryGetGroup(pointIdentity, out ItemGroupView pointGroup),
        "Point lookup failed for an existing fixture group.");
    pointChecksum += pointGroup.TotalCount + pointGroup.Buckets.Count;
}
pointWatch.Stop();
long pointAllocated = GC.GetAllocatedBytesForCurrentThread() - pointAllocatedBefore;

SnapshotDocumentBucket firstDocumentBucket = firstDocumentGroup.Buckets.First();
ItemSnapshot stagedSnapshot = new ItemSnapshot(firstDocumentGroup.AssetId,
    firstDocumentBucket.Amount, firstDocumentBucket.Quality,
    firstDocumentBucket.State ?? Array.Empty<byte>());
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long stageAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
long stageChecksum = 0;
Stopwatch stageWatch = Stopwatch.StartNew();
for (int index = 0; index < serializeIterations; index++)
{
    SnapshotDocument candidate = kernel.ExportDocument(
        new SnapshotStorageIdentity(document.WorldKey, document.PlayerKey));
    Require(SnapshotDocumentMutation.TryStoreExact(candidate, pointIdentity,
            stagedSnapshot, 1).Success,
        "Background staging mutation failed.");
    stageChecksum += candidate.Groups.Count + candidate.Groups.Sum(group => group.Buckets.Count);
}
stageWatch.Stop();
long stageAllocated = GC.GetAllocatedBytesForCurrentThread() - stageAllocatedBefore;

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long serializeAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
long serializedBytes = 0;
Stopwatch serializeWatch = Stopwatch.StartNew();
for (int index = 0; index < serializeIterations; index++)
{
    string serialized = SnapshotDocumentCodec.Serialize(kernel.ExportDocument(
        new SnapshotStorageIdentity(document.WorldKey, document.PlayerKey)));
    serializedBytes += Encoding.UTF8.GetByteCount(serialized);
}
serializeWatch.Stop();
long serializeAllocated = GC.GetAllocatedBytesForCurrentThread() - serializeAllocatedBefore;

string probeRoot = Path.Combine(probeBaseRoot, "UnturnedSnapshotPerf-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeRoot);
string probePath = Path.Combine(probeRoot, "snapshot.json");
List<double> saveMilliseconds = new List<double>();
List<double> backgroundDispatchMilliseconds = new List<double>();
List<double> backgroundTotalMilliseconds = new List<double>();
List<double> fastSaveMilliseconds = new List<double>();
List<double> fastVerifiedSaveMilliseconds = new List<double>();
try
{
    SnapshotPersistence persistence = new SnapshotPersistence(probePath);
    SnapshotSaveResult warmup = persistence.Save(document);
    Require(warmup.Success, "Warmup save failed: " + warmup.ErrorCode);
    for (int index = 0; index < saveIterations; index++)
    {
        Stopwatch saveWatch = Stopwatch.StartNew();
        SnapshotSaveResult saved = persistence.Save(document);
        saveWatch.Stop();
        Require(saved.Success, "Measured save failed: " + saved.ErrorCode);
        saveMilliseconds.Add(saveWatch.Elapsed.TotalMilliseconds);
    }
    string backgroundPath = Path.Combine(probeRoot, "background-snapshot.json");
    SnapshotPersistence backgroundPersistence = new SnapshotPersistence(backgroundPath);
    Require(backgroundPersistence.Save(document).Success, "Background warmup save failed.");
    for (int index = 0; index < saveIterations; index++)
    {
        Stopwatch totalWatch = Stopwatch.StartNew();
        Stopwatch dispatchWatch = Stopwatch.StartNew();
        SnapshotBackgroundPersistence work = SnapshotBackgroundPersistence.Start(
            document, backgroundPersistence.Save);
        dispatchWatch.Stop();
        SnapshotSaveResult saved = work.WaitForResult();
        totalWatch.Stop();
        Require(saved.Success, "Measured background save failed: " + saved.ErrorCode);
        backgroundDispatchMilliseconds.Add(dispatchWatch.Elapsed.TotalMilliseconds);
        backgroundTotalMilliseconds.Add(totalWatch.Elapsed.TotalMilliseconds);
    }
    string fastPath = Path.Combine(probeRoot, "fast-snapshot.json");
    FastAtomicSave(document, fastPath, verifyPrimary: false);
    for (int index = 0; index < saveIterations; index++)
    {
        Stopwatch saveWatch = Stopwatch.StartNew();
        FastAtomicSave(document, fastPath, verifyPrimary: false);
        saveWatch.Stop();
        fastSaveMilliseconds.Add(saveWatch.Elapsed.TotalMilliseconds);
    }
    string fastVerifiedPath = Path.Combine(probeRoot, "fast-verified-snapshot.json");
    FastAtomicSave(document, fastVerifiedPath, verifyPrimary: true);
    for (int index = 0; index < saveIterations; index++)
    {
        Stopwatch saveWatch = Stopwatch.StartNew();
        FastAtomicSave(document, fastVerifiedPath, verifyPrimary: true);
        saveWatch.Stop();
        fastVerifiedSaveMilliseconds.Add(saveWatch.Elapsed.TotalMilliseconds);
    }
    Console.WriteLine("FIXTURE=" + fixturePath);
    Console.WriteLine("FIXTURE_BYTES=" + new FileInfo(fixturePath).Length + " FIXTURE_SHA=" + Sha256(fixturePath));
    Console.WriteLine("GROUPS=" + groups + " BUCKETS=" + buckets + " INSTANCES=" + instances);
    Console.WriteLine("CLONE_ITERATIONS=" + cloneIterations +
        " TOTAL_MS=" + cloneWatch.Elapsed.TotalMilliseconds.ToString("F3") +
        " PER_CALL_MS=" + (cloneWatch.Elapsed.TotalMilliseconds / cloneIterations).ToString("F4") +
        " ALLOCATED_BYTES=" + cloneAllocated +
        " ALLOCATED_PER_CALL=" + (cloneAllocated / cloneIterations) +
        " CHECKSUM=" + cloneChecksum);
    Console.WriteLine("POINT_LOOKUP_ITERATIONS=" + cloneIterations +
        " TOTAL_MS=" + pointWatch.Elapsed.TotalMilliseconds.ToString("F3") +
        " PER_CALL_MS=" + (pointWatch.Elapsed.TotalMilliseconds / cloneIterations).ToString("F4") +
        " ALLOCATED_BYTES=" + pointAllocated +
        " ALLOCATED_PER_CALL=" + (pointAllocated / cloneIterations) +
        " CHECKSUM=" + pointChecksum);
    Console.WriteLine("BACKGROUND_STAGE_ITERATIONS=" + serializeIterations +
        " TOTAL_MS=" + stageWatch.Elapsed.TotalMilliseconds.ToString("F3") +
        " PER_CALL_MS=" + (stageWatch.Elapsed.TotalMilliseconds / serializeIterations).ToString("F4") +
        " ALLOCATED_BYTES=" + stageAllocated +
        " ALLOCATED_PER_CALL=" + (stageAllocated / serializeIterations) +
        " CHECKSUM=" + stageChecksum);
    Console.WriteLine("SERIALIZE_ITERATIONS=" + serializeIterations +
        " TOTAL_MS=" + serializeWatch.Elapsed.TotalMilliseconds.ToString("F3") +
        " PER_CALL_MS=" + (serializeWatch.Elapsed.TotalMilliseconds / serializeIterations).ToString("F4") +
        " ALLOCATED_BYTES=" + serializeAllocated +
        " ALLOCATED_PER_CALL=" + (serializeAllocated / serializeIterations) +
        " OUTPUT_BYTES=" + serializedBytes);
    Console.WriteLine("SAVE_ITERATIONS=" + saveIterations +
        " MEDIAN_MS=" + Percentile(saveMilliseconds, 0.50).ToString("F3") +
        " P95_MS=" + Percentile(saveMilliseconds, 0.95).ToString("F3") +
        " MAX_MS=" + saveMilliseconds.Max().ToString("F3") +
        " ALL_MS=" + string.Join(",", saveMilliseconds.Select(value => value.ToString("F3"))));
    Console.WriteLine("BACKGROUND_SAVE_ITERATIONS=" + saveIterations +
        " DISPATCH_MEDIAN_MS=" + Percentile(backgroundDispatchMilliseconds, 0.50).ToString("F4") +
        " DISPATCH_P95_MS=" + Percentile(backgroundDispatchMilliseconds, 0.95).ToString("F4") +
        " TOTAL_MEDIAN_MS=" + Percentile(backgroundTotalMilliseconds, 0.50).ToString("F3") +
        " TOTAL_P95_MS=" + Percentile(backgroundTotalMilliseconds, 0.95).ToString("F3"));
    Console.WriteLine("FAST_SAVE_ITERATIONS=" + saveIterations +
        " MEDIAN_MS=" + Percentile(fastSaveMilliseconds, 0.50).ToString("F3") +
        " P95_MS=" + Percentile(fastSaveMilliseconds, 0.95).ToString("F3") +
        " MAX_MS=" + fastSaveMilliseconds.Max().ToString("F3") +
        " ALL_MS=" + string.Join(",", fastSaveMilliseconds.Select(value => value.ToString("F3"))));
    Console.WriteLine("FAST_VERIFIED_SAVE_ITERATIONS=" + saveIterations +
        " MEDIAN_MS=" + Percentile(fastVerifiedSaveMilliseconds, 0.50).ToString("F3") +
        " P95_MS=" + Percentile(fastVerifiedSaveMilliseconds, 0.95).ToString("F3") +
        " MAX_MS=" + fastVerifiedSaveMilliseconds.Max().ToString("F3") +
        " ALL_MS=" + string.Join(",", fastVerifiedSaveMilliseconds.Select(value => value.ToString("F3"))));
}
finally
{
    string resolvedProbeRoot = Path.GetFullPath(probeRoot);
    if (resolvedProbeRoot.StartsWith(probeBaseRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) &&
        Directory.Exists(resolvedProbeRoot))
    {
        Directory.Delete(resolvedProbeRoot, recursive: true);
    }
}
