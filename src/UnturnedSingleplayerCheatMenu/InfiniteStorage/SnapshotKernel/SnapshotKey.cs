using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal static class SnapshotKey
{
	internal const ushort EncodingVersion = 1;

	internal static byte[] EncodeCanonical(ItemSnapshot snapshot)
	{
		if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
		byte[] state = snapshot.State;
		int length = checked(2 + 4 + 1 + 4 + 1 + 4 + state.Length);
		byte[] encoded = new byte[length];
		int offset = 0;
		BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(offset, 2), EncodingVersion);
		offset += 2;
		BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(offset, 4), 1);
		offset += 4;
		encoded[offset++] = snapshot.Amount;
		BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(offset, 4), 1);
		offset += 4;
		encoded[offset++] = snapshot.Quality;
		BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(offset, 4), state.Length);
		offset += 4;
		state.CopyTo(encoded, offset);
		return encoded;
	}

	internal static string ComputeHash(ItemSnapshot snapshot, ISnapshotHashProvider hashProvider)
	{
		if (hashProvider == null) throw new ArgumentNullException(nameof(hashProvider));
		return hashProvider.ComputeHash(EncodeCanonical(snapshot)) ?? string.Empty;
	}
}

internal sealed class Sha256SnapshotHashProvider : ISnapshotHashProvider
{
	public string ComputeHash(byte[] encodedSnapshot)
	{
		using SHA256 sha256 = SHA256.Create();
		return BitConverter.ToString(sha256.ComputeHash(encodedSnapshot ?? Array.Empty<byte>()))
			.Replace("-", string.Empty);
	}
}
