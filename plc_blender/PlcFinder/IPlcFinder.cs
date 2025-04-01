
public interface IPlcFinder{
    public abstract Task FindPlc(string? startIp, string? endIp);
    public abstract string[] GetPlcIps();
}