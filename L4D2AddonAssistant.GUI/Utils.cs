﻿using System;

namespace L4D2AddonAssistant
{
    internal static class Utils
    {
        private static readonly string[] s_byteUnits = ["B", "KiB", "MiB", "GiB", "TiB"];

        public static void GetReadableBytes(double bytes, out double value, out string unit)
        {
            value = bytes;
            int i = 0;
            while (value >= 1000 && i < s_byteUnits.Length)
            {
                value /= 1024;
                i++;
            }
            unit = s_byteUnits[i];
        }

        public static string GetReadableBytes(double bytes)
        {
            GetReadableBytes(bytes, out double value, out string unit);
            return value.ToString("F1") + unit;
        }
    }
}