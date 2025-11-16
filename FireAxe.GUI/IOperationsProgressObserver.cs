using System;

namespace FireAxe;

public interface IOperationsProgressObserver
{
    void InitOperations(int totalOperationCount, bool isCancelable);

    void NotifyOperationSucceeded(string operation);

    void NotifyOperationFailed(string operation, string failure);
}