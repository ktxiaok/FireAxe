namespace FireAxe;

public class WorkshopCollectionSyncProblem : AddonProblem
{
    public WorkshopCollectionSyncProblem(WorkshopCollectionSubscription addon, Exception exception) : base(addon)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}
