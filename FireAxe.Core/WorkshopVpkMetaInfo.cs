﻿using System;

namespace FireAxe
{
    public class WorkshopVpkMetaInfo
    {
        public required ulong PublishedFileId { get; init; }

        public required ulong TimeUpdated { get; init; }

        public required string CurrentFile { get; init; }
    }
}
