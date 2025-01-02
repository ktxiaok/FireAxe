using System;

namespace FireAxe
{
    internal class DescriptiveObject
    {
        public DescriptiveObject(string description)
        {
            Description = description;
        }

        public string Description { get; set; }

        public override string ToString() => Description;
    }
}
