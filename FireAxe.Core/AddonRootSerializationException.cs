using System;

namespace FireAxe;

public class AddonRootSerializationException : Exception
{
    public AddonRootSerializationException(string message) : base(message)
    {

    }

    public AddonRootSerializationException(string message, Exception innerException) : base(message, innerException)
    {

    }
}