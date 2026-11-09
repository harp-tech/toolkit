using System.Buffers.Binary;

namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// Represents a firmware image in the UF2 format used by the RP2040 and RP2350 microcontrollers.
/// </summary>
/// <remarks>
/// A UF2 file is a sequence of 512-byte blocks, each of which carries up to 476 bytes for one
/// target address and is tagged with the family of the targeted microcontroller. An image built
/// for the RP2350 can carry blocks for more than one family, so the image is read one family at
/// a time. Blocks that do not target the main flash are ignored.
/// </remarks>
public sealed class FirmwareImage
{
    /// <summary>
    /// The family identifier of images for the RP2040.
    /// </summary>
    public const uint FamilyRP2040 = 0xE48BFF56;

    /// <summary>
    /// The family identifier of blocks written to an absolute address on any RP2350, such as a
    /// block added by some tools to work around an RP2350 boot ROM erratum.
    /// </summary>
    public const uint FamilyAbsolute = 0xE48BFF57;

    /// <summary>
    /// The family identifier of data blocks that carry no code.
    /// </summary>
    public const uint FamilyData = 0xE48BFF58;

    /// <summary>
    /// The family identifier of secure Arm images for the RP2350.
    /// </summary>
    public const uint FamilyRP2350ArmSecure = 0xE48BFF59;

    /// <summary>
    /// The family identifier of RISC-V images for the RP2350.
    /// </summary>
    public const uint FamilyRP2350RiscV = 0xE48BFF5A;

    /// <summary>
    /// The family identifier of non-secure Arm images for the RP2350.
    /// </summary>
    public const uint FamilyRP2350ArmNonSecure = 0xE48BFF5B;

    /// <summary>
    /// The size of a flash sector, the smallest region of flash that can be erased.
    /// </summary>
    public const int SectorSize = 4096;

    const int BlockSize = 512;
    const int MaxPayloadSize = 476;
    const uint MagicStart0 = 0x0A324655;
    const uint MagicStart1 = 0x9E5D5157;
    const uint MagicEnd = 0x0AB16F30;
    const uint FlagNotMainFlash = 0x00000001;
    const uint FlagFileContainer = 0x00001000;
    const uint FlagFamilyIdPresent = 0x00002000;
    const uint FlashStart = 0x10000000;
    const uint FlashEnd = 0x12000000;

    readonly IReadOnlyList<Block> blocks;

    FirmwareImage(IReadOnlyList<Block> blocks)
    {
        this.blocks = blocks;
    }

    /// <summary>
    /// Gets the family identifiers of the blocks in the image, in order of first appearance.
    /// </summary>
    public IReadOnlyList<uint> Families => blocks.Select(block => block.Family).Distinct().ToArray();

    /// <summary>
    /// Creates a <see cref="FirmwareImage"/> object from the specified file.
    /// </summary>
    /// <param name="path">The name of the file in UF2 format.</param>
    /// <returns>A new <see cref="FirmwareImage"/> object holding the blocks of the file.</returns>
    public static FirmwareImage FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream);
    }

    /// <summary>
    /// Creates a <see cref="FirmwareImage"/> object from the specified stream.
    /// </summary>
    /// <param name="stream">The stream holding the image in UF2 format.</param>
    /// <returns>A new <see cref="FirmwareImage"/> object holding the blocks of the stream.</returns>
    /// <exception cref="ArgumentException">The stream is not a valid UF2 image.</exception>
    public static FirmwareImage FromStream(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        var blocks = new List<Block>();
        var buffer = new byte[BlockSize];
        for (int index = 0; ; index++)
        {
            var read = stream.ReadAtLeast(buffer, BlockSize, throwOnEndOfStream: false);
            if (read == 0)
            {
                break;
            }

            if (read < BlockSize)
            {
                throw new ArgumentException($"{index}: The UF2 image ends with an incomplete block.", nameof(stream));
            }

            var span = buffer.AsSpan();
            if (BinaryPrimitives.ReadUInt32LittleEndian(span) != MagicStart0 ||
                BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) != MagicStart1 ||
                BinaryPrimitives.ReadUInt32LittleEndian(span[(BlockSize - 4)..]) != MagicEnd)
            {
                throw new ArgumentException($"{index}: Invalid block marker found in the UF2 image.", nameof(stream));
            }

            var flags = BinaryPrimitives.ReadUInt32LittleEndian(span[8..]);
            var address = BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
            var payloadSize = BinaryPrimitives.ReadUInt32LittleEndian(span[16..]);
            var family = BinaryPrimitives.ReadUInt32LittleEndian(span[28..]);
            if ((flags & (FlagNotMainFlash | FlagFileContainer)) != 0)
            {
                continue;
            }

            if ((flags & FlagFamilyIdPresent) == 0)
            {
                throw new ArgumentException($"{index}: A UF2 block does not state its family.", nameof(stream));
            }

            if (payloadSize > MaxPayloadSize)
            {
                throw new ArgumentException($"{index}: Invalid payload size found in the UF2 image.", nameof(stream));
            }

            if (address < FlashStart || address + payloadSize > FlashEnd)
            {
                throw new ArgumentException($"{index}: A UF2 block targets an address outside flash.", nameof(stream));
            }

            blocks.Add(new Block(family, address, span.Slice(32, (int)payloadSize).ToArray()));
        }

        if (blocks.Count == 0)
        {
            throw new ArgumentException("The UF2 image contains no blocks for the main flash.", nameof(stream));
        }

        return new FirmwareImage(blocks);
    }

    /// <summary>
    /// Returns the flash sectors written by the blocks of the specified family, in order of address.
    /// </summary>
    /// <param name="family">The family identifier of the blocks to include.</param>
    /// <returns>
    /// The sectors written by the image. Each sector holds the contents of the image, and any byte
    /// not written by the image keeps the erased value of 0xFF.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The image writes the same byte more than once for the specified family.
    /// </exception>
    public IReadOnlyList<FlashSector> GetSectors(uint family)
    {
        var sectors = new SortedDictionary<uint, (byte[] Data, bool[] Written)>();
        foreach (var block in blocks)
        {
            if (block.Family != family)
            {
                continue;
            }

            for (int i = 0; i < block.Payload.Length; i++)
            {
                var address = block.Address + (uint)i;
                var sectorAddress = address & ~(uint)(SectorSize - 1);
                if (!sectors.TryGetValue(sectorAddress, out var sector))
                {
                    sector = (new byte[SectorSize], new bool[SectorSize]);
                    Array.Fill(sector.Data, (byte)0xFF);
                    sectors.Add(sectorAddress, sector);
                }

                var offset = (int)(address - sectorAddress);
                if (sector.Written[offset])
                {
                    throw new ArgumentException($"The UF2 image writes the byte at 0x{address:X8} more than once.");
                }

                sector.Data[offset] = block.Payload[i];
                sector.Written[offset] = true;
            }
        }

        return sectors.Select(sector => new FlashSector(sector.Key, sector.Value.Data)).ToArray();
    }

    readonly record struct Block(uint Family, uint Address, byte[] Payload);
}

/// <summary>
/// Represents the contents of one flash sector.
/// </summary>
/// <param name="Address">The address of the first byte of the sector.</param>
/// <param name="Data">The contents of the sector.</param>
public sealed record FlashSector(uint Address, byte[] Data);
