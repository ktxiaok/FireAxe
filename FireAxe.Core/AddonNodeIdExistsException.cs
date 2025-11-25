using System;

namespace FireAxe;

public class AddonNodeIdExistsException(Guid nodeId, AddonNode nodeToRegister) : Exception
{
    public Guid NodeId => nodeId;

    public AddonNode NodeToRegister => nodeToRegister;
}