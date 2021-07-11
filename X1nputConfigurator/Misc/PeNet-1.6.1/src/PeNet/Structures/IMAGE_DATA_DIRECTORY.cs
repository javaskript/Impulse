﻿using PeNet.Utilities;

namespace PeNet.Structures
{
    /// <summary>
    ///     The IMAGE_DATA_DIRECTORY struct represents the data directory,
    /// </summary>
    public class IMAGE_DATA_DIRECTORY : AbstractStructure
    {
        /// <summary>
        ///     Create a new IMAGE_DATA_DIRECTORY object.
        /// </summary>
        /// <param name="buff">PE binary as byte array.</param>
        /// <param name="offset">Raw offset to the data directory in the binary.</param>
        public IMAGE_DATA_DIRECTORY(byte[] buff, uint offset)
            : base(buff, offset)
        {
        }

        /// <summary>
        ///     RVA of the table.
        /// </summary>
        public uint VirtualAddress
        {
            get => Buff.BytesToUInt32(Offset);
            set => Buff.SetUInt32(Offset, value);
        }
    }
}