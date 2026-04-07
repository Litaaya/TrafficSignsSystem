namespace TrafficSigns.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? GetUsername();
    Guid? GetUserId();
    bool IsInRole(string role);
};
