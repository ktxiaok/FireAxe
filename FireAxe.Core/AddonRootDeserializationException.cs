using System;

namespace FireAxe;

public class AddonRootDeserializationException : Exception
{
    public AddonRootDeserializationException(string message) : base(message)
    {

    }

    public AddonRootDeserializationException(string message, Exception innerException) : base(message, innerException)
    {

    }
}