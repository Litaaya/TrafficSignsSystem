using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.Accounts.Queries;

public record AccountDto(
    Guid Id,
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System,
    bool Inactive,
    DateTime CreatedDt,
    Dictionary<string, string>? Metadata
);

public record GetAccountsQuery(
    int Page = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? StatusFilter = "all"
) : IRequest<PagedResult<AccountDto>>;

public class GetAccountsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUserService,
    IPermissionService permissionService) : IRequestHandler<GetAccountsQuery, PagedResult<AccountDto>>
{
    public async Task<PagedResult<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Accounts.AsNoTracking().AsQueryable();

        if (!permissionService.IsAdmin())
        {
            var userId = currentUserService.GetUserId();
            query = query.Where(a => a.AccountUsers.Any(au => au.UserId == userId && !au.IsDeleted));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(a => a.Name.ToLower().StartsWith(request.SearchTerm.ToLower()));
        }

        if (request.StatusFilter == "active")
        {
            query = query.Where(a => !a.IsDeleted);
        }
        else if (request.StatusFilter == "inactive")
        {
            query = query.Where(a => a.IsDeleted);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedDt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AccountDto(
                a.Id,
                a.Name,
                a.Desc,
                a.Email,
                a.Phone,
                a.System,
                a.IsDeleted,
                a.CreatedDt,
                a.Metadata))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public record GetAccountByIdQuery(Guid Id) : IRequest<AccountDto?>;

public class GetAccountByIdHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<GetAccountByIdQuery, AccountDto?>
{
    public async Task<AccountDto?> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanAccessAccountAsync(request.Id))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return await db.Accounts
            .AsNoTracking()
            .Where(a => a.Id == request.Id)
            .Select(a => new AccountDto(
                a.Id,
                a.Name,
                a.Desc,
                a.Email,
                a.Phone,
                a.System,
                a.IsDeleted,
                a.CreatedDt,
                a.Metadata))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
