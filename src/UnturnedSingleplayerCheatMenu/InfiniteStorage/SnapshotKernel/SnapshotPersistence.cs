using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal readonly struct SnapshotStorageIdentity
{
	internal SnapshotStorageIdentity(string worldKey, string playerKey)
	{
		WorldKey = worldKey ?? string.Empty;
		PlayerKey = playerKey ?? string.Empty;
	}

	internal string WorldKey { get; }
	internal string PlayerKey { get; }
	internal bool IsValid => !string.IsNullOrWhiteSpace(WorldKey) && !string.IsNullOrWhiteSpace(PlayerKey);
}

internal static class SnapshotStoragePath
{
	internal const int IdentityHashHexLength = 24;

	internal static string Resolve(string configRoot, SnapshotStorageIdentity identity)
	{
		if (string.IsNullOrWhiteSpace(configRoot)) throw new ArgumentException("A configuration root is required.", nameof(configRoot));
		if (!identity.IsValid) throw new ArgumentException("A complete storage identity is required.", nameof(identity));
		return Path.Combine(Path.GetFullPath(configRoot), "UnturnedSingleplayerCheatMenu",
			"InfiniteStorageSnapshots", "v1", HashSegment(identity.WorldKey),
			HashSegment(identity.PlayerKey), "snapshot.json");
	}

	private static string HashSegment(string value)
	{
		using SHA256 sha256 = SHA256.Create();
		return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)))
			.Replace("-", string.Empty).Substring(0, IdentityHashHexLength);
	}
}

internal sealed class SnapshotDocument
{
	internal const int CurrentSchemaVersion = 1;

	[JsonProperty(Order = 1)]
	public int SchemaVersion { get; set; } = CurrentSchemaVersion;

	[JsonProperty(Order = 2)]
	public string WorldKey { get; set; } = string.Empty;

	[JsonProperty(Order = 3)]
	public string PlayerKey { get; set; } = string.Empty;

	[JsonProperty(Order = 4)]
	public string UpdatedUtc { get; set; } = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

	[JsonProperty(Order = 5)]
	public List<SnapshotDocumentGroup> Groups { get; set; } = new List<SnapshotDocumentGroup>();

	internal static SnapshotDocument Empty(SnapshotStorageIdentity identity) => new SnapshotDocument
	{
		WorldKey = identity.WorldKey,
		PlayerKey = identity.PlayerKey
	};
}

internal sealed class SnapshotDocumentGroup
{
	[JsonProperty(Order = 1)]
	public ushort AssetId { get; set; }

	[JsonProperty(Order = 2)]
	public string AssetGuid { get; set; } = string.Empty;

	[JsonProperty(Order = 3)]
	public List<SnapshotDocumentBucket> Buckets { get; set; } = new List<SnapshotDocumentBucket>();
}

internal sealed class SnapshotDocumentBucket
{
	[JsonProperty(Order = 1)]
	public byte Amount { get; set; }

	[JsonProperty(Order = 2)]
	public byte Quality { get; set; }

	[JsonProperty(Order = 3)]
	public byte[] State { get; set; } = Array.Empty<byte>();

	[JsonProperty(Order = 4)]
	public long Count { get; set; }
}

internal sealed class SnapshotLoadResult
{
	internal bool Success { get; private set; }
	internal bool LoadedFromBackup { get; private set; }
	internal SnapshotDocument Document { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotLoadResult Succeeded(SnapshotDocument document, bool loadedFromBackup) =>
		new SnapshotLoadResult { Success = true, Document = document, LoadedFromBackup = loadedFromBackup };

	internal static SnapshotLoadResult Failed(string errorCode, SnapshotStorageIdentity identity) =>
		new SnapshotLoadResult { ErrorCode = errorCode ?? "snapshot-load-failed", Document = SnapshotDocument.Empty(identity) };
}

internal sealed class SnapshotSaveResult
{
	internal bool Success { get; private set; }
	internal bool MayHaveCommitted { get; private set; }
	internal string RecoveryBackupPath { get; private set; } = string.Empty;
	internal string CandidateRecoveryPath { get; private set; } = string.Empty;
	internal string CandidateHistoryRecoveryPath { get; private set; } = string.Empty;
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotSaveResult Succeeded(string recoveryBackupPath = "",
		string candidateRecoveryPath = "", string candidateHistoryRecoveryPath = "") =>
		new SnapshotSaveResult
		{
			Success = true,
			MayHaveCommitted = true,
			RecoveryBackupPath = recoveryBackupPath ?? string.Empty,
			CandidateRecoveryPath = candidateRecoveryPath ?? string.Empty,
			CandidateHistoryRecoveryPath = candidateHistoryRecoveryPath ?? string.Empty
		};
	internal static SnapshotSaveResult Failed(string errorCode, bool mayHaveCommitted = false,
		string recoveryBackupPath = "", string candidateRecoveryPath = "",
		string candidateHistoryRecoveryPath = "") =>
		new SnapshotSaveResult
		{
			ErrorCode = errorCode ?? "snapshot-save-failed",
			MayHaveCommitted = mayHaveCommitted,
			RecoveryBackupPath = recoveryBackupPath ?? string.Empty,
			CandidateRecoveryPath = candidateRecoveryPath ?? string.Empty,
			CandidateHistoryRecoveryPath = candidateHistoryRecoveryPath ?? string.Empty
		};
}

internal enum SnapshotVerifiedDeleteResult
{
	Deleted,
	IdentityMismatch,
	PathMismatch,
	PathOpenFailed,
	Unsupported,
	OpenFailed,
	MarkFailed,
	Exception,
	Failed
}

internal readonly struct SnapshotVerifiedDeleteOutcome
{
	internal SnapshotVerifiedDeleteOutcome(SnapshotVerifiedDeleteResult result, int nativeError = 0)
	{
		Result = result;
		NativeError = nativeError;
	}

	internal SnapshotVerifiedDeleteResult Result { get; }
	internal int NativeError { get; }

	public static implicit operator SnapshotVerifiedDeleteOutcome(SnapshotVerifiedDeleteResult result) =>
		new SnapshotVerifiedDeleteOutcome(result);
}

internal sealed class SnapshotPersistence
{
	private readonly string _path;
	private readonly Func<string, string, string, bool> _replaceOperation;
	private readonly Action<string> _faultInjection;
	private readonly Func<string, string, Action, SnapshotVerifiedDeleteOutcome> _verifiedDeleteOperation;
	private bool _loadedFromBackup;
	private bool _hasExpectedFileState;
	private SnapshotFileState _expectedPrimaryState;
	private SnapshotFileState _expectedBackupState;
	private string ReconciliationLatchPath => _path + ".reconciliation-required";
	internal bool HasReconciliationLatch => !string.IsNullOrWhiteSpace(_path) &&
		File.Exists(ReconciliationLatchPath);

	internal SnapshotPersistence(string path) : this(path, null, null, null)
	{
	}

	internal SnapshotPersistence(string path, Func<string, string, string, bool> replaceOperation) :
		this(path, replaceOperation, null, null)
	{
	}

	internal SnapshotPersistence(string path, Func<string, string, string, bool> replaceOperation,
		Action<string> faultInjection) : this(path, replaceOperation, faultInjection, null)
	{
	}

	internal SnapshotPersistence(string path, Func<string, string, string, bool> replaceOperation,
		Action<string> faultInjection,
		Func<string, string, Action, SnapshotVerifiedDeleteOutcome> verifiedDeleteOperation)
	{
		_path = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
		_replaceOperation = replaceOperation ?? ReplaceFile;
		_faultInjection = faultInjection;
		_verifiedDeleteOperation = verifiedDeleteOperation ?? DeleteVerifiedFileByIdentity;
	}

	internal SnapshotLoadResult Load(SnapshotStorageIdentity identity)
	{
		_loadedFromBackup = false;
		_hasExpectedFileState = false;
		if (string.IsNullOrWhiteSpace(_path) || !identity.IsValid)
			return SnapshotLoadResult.Failed("invalid-storage-identity", identity);
		if (HasReconciliationLatch)
			return SnapshotLoadResult.Failed("reconciliation-required", identity);
		SnapshotFileState primaryState;
		SnapshotFileState backupState;
		try
		{
			primaryState = SnapshotFileState.Capture(_path);
			backupState = SnapshotFileState.Capture(_path + ".bak");
		}
		catch (Exception)
		{
			return SnapshotLoadResult.Failed("load-state-capture-failed", identity);
		}

		if (!primaryState.Exists)
		{
			if (!backupState.Exists)
			{
				if (!TryAcceptLoadBaseline(primaryState, backupState, out string emptyLoadError))
					return SnapshotLoadResult.Failed(emptyLoadError, identity);
				return SnapshotLoadResult.Succeeded(SnapshotDocument.Empty(identity), false);
			}
			if (!TryRead(_path + ".bak", identity, out SnapshotDocument backupOnly))
				return SnapshotLoadResult.Failed("backup-invalid", identity);
			if (!TryAcceptLoadBaseline(primaryState, backupState, out string backupOnlyError))
				return SnapshotLoadResult.Failed(backupOnlyError, identity);
			_loadedFromBackup = true;
			return SnapshotLoadResult.Succeeded(backupOnly, true);
		}

		if (TryRead(_path, identity, out SnapshotDocument primary))
		{
			if (!TryAcceptLoadBaseline(primaryState, backupState, out string primaryLoadError))
				return SnapshotLoadResult.Failed(primaryLoadError, identity);
			return SnapshotLoadResult.Succeeded(primary, false);
		}
		if (!TryRead(_path + ".bak", identity, out SnapshotDocument backup))
			return SnapshotLoadResult.Failed("primary-and-backup-invalid", identity);
		if (!TryAcceptLoadBaseline(primaryState, backupState, out string fallbackLoadError))
			return SnapshotLoadResult.Failed(fallbackLoadError, identity);
		_loadedFromBackup = true;
		return SnapshotLoadResult.Succeeded(backup, true);
	}

	internal bool TryCreateReconciliationLatch(string reason)
	{
		if (string.IsNullOrWhiteSpace(_path)) return false;
		string directory = Path.GetDirectoryName(_path);
		if (string.IsNullOrWhiteSpace(directory)) return false;
		try
		{
			Directory.CreateDirectory(directory);
			if (File.Exists(ReconciliationLatchPath)) return true;
			string normalizedReason = (reason ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
			if (normalizedReason.Length > 2048) normalizedReason = normalizedReason.Substring(0, 2048);
			byte[] bytes = new UTF8Encoding(false).GetBytes(
				"Snapshot reconciliation required\nCreatedUtc=" +
				DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\nReason=" + normalizedReason + "\n");
			using FileStream stream = new FileStream(ReconciliationLatchPath, FileMode.CreateNew,
				FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
			stream.Write(bytes, 0, bytes.Length);
			stream.Flush(true);
			return true;
		}
		catch (IOException)
		{
			return File.Exists(ReconciliationLatchPath);
		}
		catch (Exception)
		{
			return false;
		}
	}

	internal SnapshotSaveResult Save(SnapshotDocument document)
	{
		if (string.IsNullOrWhiteSpace(_path)) return SnapshotSaveResult.Failed("invalid-storage-path");
		if (HasReconciliationLatch) return SnapshotSaveResult.Failed("reconciliation-required");
		if (!SnapshotDocumentValidator.TryValidate(document, out string validationError))
			return SnapshotSaveResult.Failed(validationError);

		string directory = Path.GetDirectoryName(_path);
		if (string.IsNullOrWhiteSpace(directory)) return SnapshotSaveResult.Failed("invalid-storage-path");
		string temporaryPath = string.Empty;
		string temporarySha256 = string.Empty;
		string recoveryBackupPath = string.Empty;
		string candidateRecoveryPath = string.Empty;
		string candidateHistoryRecoveryPath = string.Empty;
		byte[] candidateBytes = Array.Empty<byte>();
		bool replacementStarted = false;
		try
		{
			Directory.CreateDirectory(directory);
			string backupPath = _path + ".bak";
			SnapshotFileState primaryBefore = _hasExpectedFileState
				? _expectedPrimaryState
				: SnapshotFileState.Capture(_path);
			SnapshotFileState backupBefore = _hasExpectedFileState
				? _expectedBackupState
				: SnapshotFileState.Capture(backupPath);
			string stableCandidatePath = _path + ".candidate.latest";
			string previousCandidatePath = _path + ".candidate.previous";
			SnapshotFileState stableCandidateBefore = SnapshotFileState.Capture(stableCandidatePath);
			SnapshotFileState previousCandidateBefore = SnapshotFileState.Capture(previousCandidatePath);
			string json = SnapshotDocumentCodec.Serialize(document);
			if (!SnapshotDocumentValidator.TryParse(json, out _, out validationError))
				return SnapshotSaveResult.Failed(validationError);

			temporaryPath = _path + ".write-" + Guid.NewGuid().ToString("N") + ".tmp";
			candidateBytes = new UTF8Encoding(false).GetBytes(json);
			using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
				FileShare.None, 4096, FileOptions.WriteThrough))
			{
				stream.Write(candidateBytes, 0, candidateBytes.Length);
				stream.Flush(true);
			}
			temporarySha256 = ComputeSha256(candidateBytes);
			if (!TryRead(temporaryPath, new SnapshotStorageIdentity(document.WorldKey, document.PlayerKey), out _) ||
				!string.Equals(ComputeSha256(temporaryPath), temporarySha256, StringComparison.Ordinal))
				return SnapshotSaveResult.Failed("temporary-verification-failed");
			candidateRecoveryPath = WriteForensicCandidate(candidateBytes, temporarySha256);
			if (string.IsNullOrWhiteSpace(candidateRecoveryPath))
				return SnapshotSaveResult.Failed("candidate-persistence-failed",
					candidateRecoveryPath: ExistingVerifiedCandidate(temporaryPath, temporarySha256));

			_faultInjection?.Invoke("before-replace");
			if (!File.Exists(temporaryPath) || !string.Equals(ComputeSha256(temporaryPath),
				temporarySha256, StringComparison.Ordinal)) return SnapshotSaveResult.Failed("temporary-drift",
					candidateRecoveryPath: candidateRecoveryPath);
			if (!primaryBefore.StillMatches(_path)) return SnapshotSaveResult.Failed("primary-drift",
				candidateRecoveryPath: candidateRecoveryPath);
			if (!backupBefore.StillMatches(backupPath)) return SnapshotSaveResult.Failed("backup-drift",
				candidateRecoveryPath: candidateRecoveryPath);
			if (HasReconciliationLatch) return SnapshotSaveResult.Failed("reconciliation-required",
				candidateRecoveryPath: candidateRecoveryPath);
			bool replaced;
			replacementStarted = true;
			if (primaryBefore.Exists)
			{
				recoveryBackupPath = _path + ".pre-replace-" + Guid.NewGuid().ToString("N") + ".bak";
				replaced = _replaceOperation(temporaryPath, _path, recoveryBackupPath);
			}
			else
			{
				File.Move(temporaryPath, _path);
				replaced = true;
			}
			if (!replaced)
			{
				return primaryBefore.StillMatches(_path) && backupBefore.StillMatches(backupPath)
					? SnapshotSaveResult.Failed("atomic-replacement-rejected",
						candidateRecoveryPath: ExistingVerifiedCandidate(temporaryPath, temporarySha256))
					: FailAfterReplacement("atomic-replacement-conflict", temporaryPath,
						temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			}
			if (!File.Exists(temporaryPath)) temporaryPath = string.Empty;
			_faultInjection?.Invoke("after-replace-before-verification");
			if (HasReconciliationLatch)
				return FailAfterReplacement("reconciliation-required", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			if (!File.Exists(_path) || !string.Equals(ComputeSha256(_path), temporarySha256, StringComparison.Ordinal))
				return FailAfterReplacement("saved-file-verification-failed", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			if (primaryBefore.Exists)
			{
				if (!File.Exists(recoveryBackupPath))
					return FailAfterReplacement("pre-replace-backup-missing", temporaryPath,
						temporarySha256, recoveryBackupPath, candidateRecoveryPath);
				string instantPrimarySha256 = ComputeSha256(recoveryBackupPath);
				if (!string.Equals(instantPrimarySha256, primaryBefore.Sha256, StringComparison.Ordinal))
					return FailAfterReplacement("primary-changed-at-replace", temporaryPath,
						temporarySha256, recoveryBackupPath, candidateRecoveryPath);
				if (_loadedFromBackup)
				{
					if (!backupBefore.StillMatches(backupPath))
						return FailAfterReplacement("backup-preservation-failed", temporaryPath,
							temporarySha256, recoveryBackupPath, candidateRecoveryPath);
				}
				else
				{
					if (!backupBefore.StillMatches(backupPath))
						return FailAfterReplacement("backup-drift-after-replace", temporaryPath,
							temporarySha256, recoveryBackupPath, candidateRecoveryPath);
					PromoteRecoveryBackup(recoveryBackupPath, backupPath, backupBefore.Exists);
					recoveryBackupPath = backupPath;
					if (!File.Exists(backupPath) || !string.Equals(ComputeSha256(backupPath),
						primaryBefore.Sha256, StringComparison.Ordinal))
						return FailAfterReplacement("backup-rotation-verification-failed",
							temporaryPath, temporarySha256, backupPath, candidateRecoveryPath);
				}
			}
			_faultInjection?.Invoke("after-backup-promotion-before-final-verification");
			if (HasReconciliationLatch)
				return FailAfterReplacement("reconciliation-required", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			if (!File.Exists(_path) || !string.Equals(ComputeSha256(_path), temporarySha256,
				StringComparison.Ordinal))
				return FailAfterReplacement("saved-file-verification-failed", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			_faultInjection?.Invoke("before-candidate-history-promotion");
			if (!stableCandidateBefore.StillMatches(stableCandidatePath) ||
				!previousCandidateBefore.StillMatches(previousCandidatePath))
				return FailAfterReplacement("candidate-history-drift", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			_faultInjection?.Invoke("during-candidate-history-promotion");
			if (!stableCandidateBefore.StillMatches(stableCandidatePath) ||
				!previousCandidateBefore.StillMatches(previousCandidatePath))
				return FailAfterReplacement("candidate-history-drift", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			if (!TryPromoteCandidateRecovery(candidateRecoveryPath, temporarySha256,
				stableCandidateBefore, previousCandidateBefore, out string promotedCandidatePath,
				out candidateHistoryRecoveryPath, out string promotionError))
			{
				candidateRecoveryPath = promotedCandidatePath;
				return FailAfterReplacement(promotionError, temporaryPath, temporarySha256,
					recoveryBackupPath, candidateRecoveryPath, candidateHistoryRecoveryPath);
			}
			candidateRecoveryPath = promotedCandidatePath;
			if (stableCandidateBefore.Exists)
			{
				if (!File.Exists(previousCandidatePath) || !string.Equals(
					ComputeSha256(previousCandidatePath), stableCandidateBefore.Sha256,
					StringComparison.Ordinal))
					return FailAfterReplacement("candidate-history-verification-failed",
						temporaryPath, temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			}
			else if (!previousCandidateBefore.StillMatches(previousCandidatePath))
			{
				return FailAfterReplacement("candidate-history-verification-failed",
					temporaryPath, temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			}
			string successfulRecoveryPath = ExistingRecoveryPath(recoveryBackupPath);
			_faultInjection?.Invoke("after-candidate-history-promotion-before-success");
			if (HasReconciliationLatch)
				return FailAfterReplacement("reconciliation-required", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			if (!File.Exists(_path) || !string.Equals(ComputeSha256(_path), temporarySha256,
				StringComparison.Ordinal))
				return FailAfterReplacement("saved-file-verification-failed", temporaryPath,
					temporarySha256, recoveryBackupPath, candidateRecoveryPath);
			_expectedPrimaryState = SnapshotFileState.Known(true, temporarySha256);
			_expectedBackupState = _loadedFromBackup
				? backupBefore
				: primaryBefore.Exists ? SnapshotFileState.Known(true, primaryBefore.Sha256) : backupBefore;
			_hasExpectedFileState = true;
			_loadedFromBackup = false;
			return SnapshotSaveResult.Succeeded(successfulRecoveryPath);
		}
		catch (Exception exception)
		{
			return replacementStarted
				? FailAfterReplacement("save-failed:" + exception.GetType().Name,
					temporaryPath, temporarySha256, recoveryBackupPath, candidateRecoveryPath,
					candidateHistoryRecoveryPath)
				: SnapshotSaveResult.Failed("save-failed:" + exception.GetType().Name,
					candidateRecoveryPath: ExistingVerifiedCandidate(candidateRecoveryPath,
						temporarySha256));
		}
	}

	private SnapshotSaveResult FailAfterReplacement(string errorCode, string temporaryPath,
		string candidateSha256, string recoveryBackupPath, string candidateRecoveryPath,
		string candidateHistoryRecoveryPath = "")
	{
		candidateRecoveryPath = FindCandidateRecovery(candidateRecoveryPath,
			temporaryPath, candidateSha256);
		if (string.IsNullOrWhiteSpace(candidateRecoveryPath))
			errorCode += ":candidate-recovery-failed";
		return SnapshotSaveResult.Failed(errorCode, mayHaveCommitted: true,
			recoveryBackupPath: ExistingRecoveryPath(recoveryBackupPath),
			candidateRecoveryPath: candidateRecoveryPath,
			candidateHistoryRecoveryPath: ExistingRecoveryPath(candidateHistoryRecoveryPath));
	}

	private string FindCandidateRecovery(string candidateRecoveryPath,
		string temporaryPath, string candidateSha256)
	{
		string existing = ExistingVerifiedCandidate(candidateRecoveryPath, candidateSha256);
		if (!string.IsNullOrWhiteSpace(existing)) return existing;
		existing = ExistingVerifiedCandidate(_path + ".candidate.latest", candidateSha256);
		if (!string.IsNullOrWhiteSpace(existing)) return existing;
		return ExistingVerifiedCandidate(temporaryPath, candidateSha256);
	}

	private string WriteForensicCandidate(byte[] bytes, string expectedSha256)
	{
		if (bytes == null || bytes.Length == 0 || string.IsNullOrWhiteSpace(expectedSha256)) return string.Empty;
		string path = _path + ".candidate-recovery-" + Guid.NewGuid().ToString("N") + ".json";
		try
		{
			_faultInjection?.Invoke("before-candidate-persist|" + path);
			using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
				FileShare.Read, 4096, FileOptions.WriteThrough))
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush(true);
			}
			return ExistingVerifiedCandidate(path, expectedSha256);
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private bool TryPromoteCandidateRecovery(string candidateRecoveryPath, string expectedSha256,
		SnapshotFileState stableCandidateBefore, SnapshotFileState previousCandidateBefore,
		out string promotedCandidatePath, out string candidateHistoryRecoveryPath,
		out string errorCode)
	{
		promotedCandidatePath = candidateRecoveryPath;
		candidateHistoryRecoveryPath = string.Empty;
		errorCode = "candidate-promotion-failed";
		if (string.IsNullOrWhiteSpace(ExistingVerifiedCandidate(candidateRecoveryPath,
			expectedSha256))) return false;
		string stableCandidatePath = _path + ".candidate.latest";
		string previousCandidatePath = _path + ".candidate.previous";
		_faultInjection?.Invoke("after-final-candidate-history-check-before-replace");
		if (!stableCandidateBefore.Exists)
		{
			File.Move(candidateRecoveryPath, stableCandidatePath);
			promotedCandidatePath = ExistingVerifiedCandidate(stableCandidatePath, expectedSha256);
			return !string.IsNullOrWhiteSpace(promotedCandidatePath);
		}

		string latestRecoveryPath = _path + ".candidate-history-latest-recovery-" +
			Guid.NewGuid().ToString("N") + ".bak";
		candidateHistoryRecoveryPath = latestRecoveryPath;
		File.Replace(candidateRecoveryPath, stableCandidatePath, latestRecoveryPath,
			ignoreMetadataErrors: true);
		promotedCandidatePath = ExistingVerifiedCandidate(stableCandidatePath, expectedSha256);
		if (string.IsNullOrWhiteSpace(promotedCandidatePath) || !File.Exists(latestRecoveryPath))
		{
			errorCode = "candidate-history-verification-failed";
			return false;
		}
		if (!string.Equals(ComputeSha256(latestRecoveryPath), stableCandidateBefore.Sha256,
			StringComparison.Ordinal))
		{
			errorCode = "candidate-history-drift";
			return false;
		}

		if (previousCandidateBefore.Exists)
		{
			string previousRecoveryPath = _path + ".candidate-history-previous-recovery-" +
				Guid.NewGuid().ToString("N") + ".bak";
			candidateHistoryRecoveryPath = previousRecoveryPath;
			File.Replace(latestRecoveryPath, previousCandidatePath, previousRecoveryPath,
				ignoreMetadataErrors: true);
			SnapshotVerifiedDeleteOutcome cleanup = _verifiedDeleteOperation(previousRecoveryPath,
				previousCandidateBefore.Sha256, () => _faultInjection?.Invoke(
					"after-candidate-history-cleanup-check-before-delete|" + previousRecoveryPath));
			if (cleanup.Result != SnapshotVerifiedDeleteResult.Deleted)
			{
				errorCode = cleanup.Result switch
				{
					SnapshotVerifiedDeleteResult.IdentityMismatch => "candidate-history-drift",
					SnapshotVerifiedDeleteResult.PathMismatch => "candidate-history-cleanup-drift",
					SnapshotVerifiedDeleteResult.PathOpenFailed => "candidate-history-cleanup-path-open-failed",
					SnapshotVerifiedDeleteResult.Unsupported => "candidate-history-cleanup-unsupported",
					SnapshotVerifiedDeleteResult.OpenFailed => "candidate-history-cleanup-open-failed",
					SnapshotVerifiedDeleteResult.MarkFailed => "candidate-history-cleanup-mark-failed",
					SnapshotVerifiedDeleteResult.Exception => "candidate-history-cleanup-exception",
					_ => "candidate-history-cleanup-failed"
				};
				if (cleanup.NativeError != 0) errorCode += ":win32-" + cleanup.NativeError;
				return false;
			}
		}
		else
			File.Move(latestRecoveryPath, previousCandidatePath);
		candidateHistoryRecoveryPath = string.Empty;
		if (!File.Exists(previousCandidatePath) || !string.Equals(ComputeSha256(previousCandidatePath),
			stableCandidateBefore.Sha256, StringComparison.Ordinal))
		{
			errorCode = "candidate-history-verification-failed";
			return false;
		}
		return true;
	}

	private static SnapshotVerifiedDeleteOutcome DeleteVerifiedFileByIdentity(string path,
		string expectedSha256, Action protectedWindow)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return SnapshotVerifiedDeleteResult.Unsupported;
		if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expectedSha256))
			return SnapshotVerifiedDeleteResult.Failed;
		try
		{
			SafeFileHandle handle = CreateFileW(ToExtendedWindowsPath(path),
				GenericRead | DeleteAccess, FileShareRead,
				IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
			if (handle == null || handle.IsInvalid)
			{
				int openError = Marshal.GetLastWin32Error();
				handle?.Dispose();
				return new SnapshotVerifiedDeleteOutcome(SnapshotVerifiedDeleteResult.OpenFailed,
					openError);
			}
			return WithOwnedReadHandle(handle,
				ownedHandle => new FileStream(ownedHandle, FileAccess.Read), stream =>
			{
				if (!string.Equals(ComputeSha256(stream), expectedSha256, StringComparison.Ordinal))
					return SnapshotVerifiedDeleteResult.IdentityMismatch;
				protectedWindow?.Invoke();
				if (!string.Equals(ComputeSha256(stream), expectedSha256, StringComparison.Ordinal))
					return SnapshotVerifiedDeleteResult.IdentityMismatch;
				SafeFileHandle pathHandle = CreateFileW(ToExtendedWindowsPath(path), GenericRead,
					FileShareRead | FileShareDelete, IntPtr.Zero, OpenExisting,
					FileAttributeNormal, IntPtr.Zero);
				if (pathHandle == null || pathHandle.IsInvalid)
				{
					int pathOpenError = Marshal.GetLastWin32Error();
					pathHandle?.Dispose();
					return new SnapshotVerifiedDeleteOutcome(
						SnapshotVerifiedDeleteResult.PathOpenFailed, pathOpenError);
				}
				SnapshotVerifiedDeleteOutcome pathVerification = WithOwnedReadHandle(pathHandle,
					ownedHandle => new FileStream(ownedHandle, FileAccess.Read), pathStream =>
				{
					if (!string.Equals(ComputeSha256(pathStream), expectedSha256,
						StringComparison.Ordinal)) return SnapshotVerifiedDeleteResult.PathMismatch;
					return SnapshotVerifiedDeleteResult.Deleted;
				});
				if (pathVerification.Result != SnapshotVerifiedDeleteResult.Deleted)
					return pathVerification;
				FileDispositionInfo disposition = new FileDispositionInfo { DeleteFile = 1 };
				bool markedForDelete = SetFileInformationByHandle(stream.SafeFileHandle,
					FileInfoByHandleClass.FileDispositionInfo, ref disposition,
					(uint)Marshal.SizeOf(typeof(FileDispositionInfo)));
				if (markedForDelete) return SnapshotVerifiedDeleteResult.Deleted;
				int markError = Marshal.GetLastWin32Error();
				return new SnapshotVerifiedDeleteOutcome(SnapshotVerifiedDeleteResult.MarkFailed,
					markError);
			});
		}
		catch (Exception)
		{
			return SnapshotVerifiedDeleteResult.Exception;
		}
	}

	internal static SnapshotVerifiedDeleteOutcome WithOwnedReadHandle(SafeFileHandle handle,
		Func<SafeFileHandle, FileStream> streamFactory,
		Func<FileStream, SnapshotVerifiedDeleteOutcome> operation)
	{
		if (handle == null) throw new ArgumentNullException(nameof(handle));
		if (streamFactory == null) throw new ArgumentNullException(nameof(streamFactory));
		if (operation == null) throw new ArgumentNullException(nameof(operation));
		using (handle)
		using (FileStream stream = streamFactory(handle))
			return operation(stream);
	}

	private static string ToExtendedWindowsPath(string path)
	{
		string fullPath = Path.GetFullPath(path);
		if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal)) return fullPath;
		return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
			? @"\\?\UNC\" + fullPath.Substring(2)
			: @"\\?\" + fullPath;
	}

	private static string ComputeSha256(FileStream stream)
	{
		stream.Position = 0;
		using SHA256 sha256 = SHA256.Create();
		return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
	}

	private const uint GenericRead = 0x80000000;
	private const uint DeleteAccess = 0x00010000;
	private const uint FileShareRead = 0x00000001;
	private const uint FileShareDelete = 0x00000004;
	private const uint OpenExisting = 3;
	private const uint FileAttributeNormal = 0x00000080;

	private enum FileInfoByHandleClass
	{
		FileDispositionInfo = 4
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct FileDispositionInfo
	{
		internal byte DeleteFile;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess,
		uint shareMode, IntPtr securityAttributes, uint creationDisposition,
		uint flagsAndAttributes, IntPtr templateFile);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetFileInformationByHandle(SafeFileHandle file,
		FileInfoByHandleClass fileInformationClass, ref FileDispositionInfo fileInformation,
		uint bufferSize);

	private static string ExistingVerifiedCandidate(string path, string expectedSha256)
	{
		try
		{
			return !string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(expectedSha256) &&
				File.Exists(path) && string.Equals(ComputeSha256(path), expectedSha256,
					StringComparison.Ordinal) ? path : string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private static void PromoteRecoveryBackup(string recoveryBackupPath,
		string stableBackupPath, bool stableBackupExisted)
	{
		if (stableBackupExisted)
			File.Replace(recoveryBackupPath, stableBackupPath, stableBackupPath + ".previous",
				ignoreMetadataErrors: true);
		else
			File.Move(recoveryBackupPath, stableBackupPath);
	}

	private static string ExistingRecoveryPath(string path)
	{
		try { return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : string.Empty; }
		catch (Exception) { return string.Empty; }
	}

	private static bool TryRead(string path, SnapshotStorageIdentity identity, out SnapshotDocument document)
	{
		document = null;
		if (!File.Exists(path)) return false;
		try
		{
			string json = File.ReadAllText(path, Encoding.UTF8);
			if (!SnapshotDocumentValidator.TryParse(json, out document, out _)) return false;
			return string.Equals(document.WorldKey, identity.WorldKey, StringComparison.Ordinal) &&
				string.Equals(document.PlayerKey, identity.PlayerKey, StringComparison.Ordinal);
		}
		catch (Exception)
		{
			document = null;
			return false;
		}
	}

	private bool TryAcceptLoadBaseline(SnapshotFileState primaryState,
		SnapshotFileState backupState, out string errorCode)
	{
		errorCode = string.Empty;
		try
		{
			_faultInjection?.Invoke("before-load-accept");
			if (HasReconciliationLatch)
			{
				errorCode = "reconciliation-required";
				return false;
			}
			if (!primaryState.StillMatches(_path) || !backupState.StillMatches(_path + ".bak"))
			{
				errorCode = "load-file-drift";
				return false;
			}
			_expectedPrimaryState = primaryState;
			_expectedBackupState = backupState;
			_hasExpectedFileState = true;
			return true;
		}
		catch (Exception)
		{
			errorCode = "load-file-drift";
			return false;
		}
	}

	private static bool ReplaceFile(string sourcePath, string destinationPath, string backupPath)
	{
		File.Replace(sourcePath, destinationPath,
			string.IsNullOrWhiteSpace(backupPath) ? null : backupPath, ignoreMetadataErrors: true);
		return true;
	}

	private static string ComputeSha256(string path)
	{
		using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		using SHA256 sha256 = SHA256.Create();
		return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
	}

	private static string ComputeSha256(byte[] bytes)
	{
		using SHA256 sha256 = SHA256.Create();
		return BitConverter.ToString(sha256.ComputeHash(bytes ?? Array.Empty<byte>())).Replace("-", string.Empty);
	}

	private readonly struct SnapshotFileState
	{
		private SnapshotFileState(bool exists, string sha256)
		{
			Exists = exists;
			Sha256 = sha256 ?? string.Empty;
		}

		internal bool Exists { get; }
		internal string Sha256 { get; }

		internal static SnapshotFileState Capture(string path) => File.Exists(path)
			? new SnapshotFileState(true, ComputeSha256(path))
			: new SnapshotFileState(false, string.Empty);

		internal static SnapshotFileState Known(bool exists, string sha256) =>
			new SnapshotFileState(exists, sha256);

		internal bool StillMatches(string path)
		{
			if (File.Exists(path) != Exists) return false;
			return !Exists || string.Equals(ComputeSha256(path), Sha256, StringComparison.Ordinal);
		}
	}
}

internal static class SnapshotDocumentValidator
{
	private static readonly string[] RootProperties = { "SchemaVersion", "WorldKey", "PlayerKey", "UpdatedUtc", "Groups" };
	private static readonly string[] GroupProperties = { "AssetId", "AssetGuid", "Buckets" };
	private static readonly string[] BucketProperties = { "Amount", "Quality", "State", "Count" };

	internal static bool TryParse(string json, out SnapshotDocument document, out string errorCode)
	{
		document = null;
		errorCode = string.Empty;
		try
		{
			if (ContainsJsonComment(json))
			{
				errorCode = "snapshot-json-comments-not-allowed";
				return false;
			}
			using StringReader textReader = new StringReader(json ?? string.Empty);
			using JsonTextReader jsonReader = new JsonTextReader(textReader) { DateParseHandling = DateParseHandling.None };
			JObject root = JObject.Load(jsonReader, new JsonLoadSettings
			{
				DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
				CommentHandling = CommentHandling.Load
			});
			if (jsonReader.Read())
			{
				errorCode = "invalid-snapshot-json";
				return false;
			}
			if (!HasExactProperties(root, RootProperties) || !TryInteger(root["SchemaVersion"], out long schema) ||
				schema != SnapshotDocument.CurrentSchemaVersion || root["WorldKey"]?.Type != JTokenType.String ||
				root["PlayerKey"]?.Type != JTokenType.String || root["UpdatedUtc"]?.Type != JTokenType.String ||
				root["Groups"] is not JArray groupsToken)
			{
				errorCode = "invalid-snapshot-root";
				return false;
			}
			string worldKey = root["WorldKey"].Value<string>();
			string playerKey = root["PlayerKey"].Value<string>();
			string updatedUtc = root["UpdatedUtc"].Value<string>();
			if (string.IsNullOrWhiteSpace(worldKey) || string.IsNullOrWhiteSpace(playerKey) ||
				!DateTime.TryParseExact(updatedUtc, "O", CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind, out _))
			{
				errorCode = "invalid-snapshot-identity";
				return false;
			}

			SnapshotDocument candidate = new SnapshotDocument
			{
				WorldKey = worldKey,
				PlayerKey = playerKey,
				UpdatedUtc = updatedUtc,
				Groups = new List<SnapshotDocumentGroup>()
			};
			HashSet<ushort> assetIds = new HashSet<ushort>();
			foreach (JToken groupToken in groupsToken)
			{
				if (groupToken is not JObject groupObject || !HasExactProperties(groupObject, GroupProperties) ||
					!TryInteger(groupObject["AssetId"], out long assetIdValue) || assetIdValue <= 0 ||
					assetIdValue > ushort.MaxValue || groupObject["AssetGuid"]?.Type != JTokenType.String ||
					groupObject["Buckets"] is not JArray bucketsToken)
				{
					errorCode = "invalid-snapshot-group";
					return false;
				}
				ushort assetId = (ushort)assetIdValue;
				if (!assetIds.Add(assetId) || !Guid.TryParseExact(groupObject["AssetGuid"].Value<string>(), "D", out Guid assetGuid) ||
					assetGuid == Guid.Empty || bucketsToken.Count == 0)
				{
					errorCode = "invalid-snapshot-group";
					return false;
				}
				SnapshotDocumentGroup group = new SnapshotDocumentGroup
				{
					AssetId = assetId,
					AssetGuid = assetGuid.ToString("D"),
					Buckets = new List<SnapshotDocumentBucket>()
				};
				foreach (JToken bucketToken in bucketsToken)
				{
					if (bucketToken is not JObject bucketObject || !HasExactProperties(bucketObject, BucketProperties) ||
						!TryInteger(bucketObject["Amount"], out long amount) || amount < byte.MinValue || amount > byte.MaxValue ||
						!TryInteger(bucketObject["Quality"], out long quality) || quality < byte.MinValue || quality > byte.MaxValue ||
						bucketObject["State"]?.Type != JTokenType.String ||
						!TryInteger(bucketObject["Count"], out long count) || count <= 0)
					{
						errorCode = "invalid-snapshot-bucket";
						return false;
					}
					byte[] state;
					try { state = Convert.FromBase64String(bucketObject["State"].Value<string>() ?? string.Empty); }
					catch (FormatException)
					{
						errorCode = "invalid-snapshot-state";
						return false;
					}
					group.Buckets.Add(new SnapshotDocumentBucket
					{
						Amount = (byte)amount,
						Quality = (byte)quality,
						State = state,
						Count = count
					});
				}
				candidate.Groups.Add(group);
			}
			document = candidate;
			return true;
		}
		catch (Exception)
		{
			errorCode = "invalid-snapshot-json";
			return false;
		}
	}

	internal static bool TryValidate(SnapshotDocument document, out string errorCode)
	{
		if (document == null)
		{
			errorCode = "snapshot-document-null";
			return false;
		}
		try
		{
			return TryParse(SnapshotDocumentCodec.Serialize(document), out _, out errorCode);
		}
		catch (Exception exception)
		{
			errorCode = "invalid-snapshot-document:" + exception.GetType().Name + ":" + exception.Message;
			return false;
		}
	}

	private static bool HasExactProperties(JObject value, IEnumerable<string> expected)
	{
		string[] actual = value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
		string[] required = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
		return actual.SequenceEqual(required, StringComparer.Ordinal);
	}

	private static bool ContainsJsonComment(string json)
	{
		using StringReader textReader = new StringReader(json ?? string.Empty);
		using JsonTextReader jsonReader = new JsonTextReader(textReader)
		{
			DateParseHandling = DateParseHandling.None
		};
		while (jsonReader.Read())
			if (jsonReader.TokenType == JsonToken.Comment) return true;
		return false;
	}

	private static bool TryInteger(JToken token, out long value)
	{
		value = 0;
		if (token?.Type != JTokenType.Integer) return false;
		try
		{
			value = token.Value<long>();
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}

internal static class SnapshotDocumentCodec
{
	internal static string Serialize(SnapshotDocument document)
	{
		if (document == null) throw new ArgumentNullException(nameof(document));
		JArray groups = new JArray();
		foreach (SnapshotDocumentGroup group in document.Groups ?? throw new InvalidOperationException("Groups are required."))
		{
			if (group == null) throw new InvalidOperationException("Group entries cannot be null.");
			JArray buckets = new JArray();
			foreach (SnapshotDocumentBucket bucket in group.Buckets ?? throw new InvalidOperationException("Buckets are required."))
			{
				if (bucket == null) throw new InvalidOperationException("Bucket entries cannot be null.");
				buckets.Add(new JObject
				{
					["Amount"] = bucket.Amount,
					["Quality"] = bucket.Quality,
					["State"] = Convert.ToBase64String(bucket.State ?? throw new InvalidOperationException("State is required.")),
					["Count"] = bucket.Count
				});
			}
			groups.Add(new JObject
			{
				["AssetId"] = group.AssetId,
				["AssetGuid"] = group.AssetGuid,
				["Buckets"] = buckets
			});
		}
		return new JObject
		{
			["SchemaVersion"] = document.SchemaVersion,
			["WorldKey"] = document.WorldKey,
			["PlayerKey"] = document.PlayerKey,
			["UpdatedUtc"] = document.UpdatedUtc,
			["Groups"] = groups
		}.ToString(Formatting.Indented);
	}
}
