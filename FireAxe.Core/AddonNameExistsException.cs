﻿using System;

namespace FireAxe
{
    public class AddonNameExistsException : Exception
    {
        public AddonNameExistsException(string addonName)
        {
            ArgumentNullException.ThrowIfNull(addonName);
            AddonName = addonName;
        }
        public string AddonName { get; }
    }
}
