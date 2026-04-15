using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using System.Collections.Generic;

namespace TrafficSigns.Application.Features.Users.Queries;

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string? Phone,
    string? FirstName,
    string? LastName,
    bool Inactive,
    DateTime CreatedDt,
    Dictionary<string, string>? Metadata
);

public record GetUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? StatusFilter = "all"
) : IRequest<PagedResult<UserDto>>;

public class GetUsersHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
            throw new UnauthorizedAccessException("Access denied");

        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();

            var exactMatchId = await db.Users.AsNoTracking()
                .Where(u => u.Username.ToLower() == term
                         || (u.Email != null && u.Email.ToLower() == term)
                         || u.Phone == term)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (exactMatchId != Guid.Empty)
            {
                query = query.Where(u => u.Id == exactMatchId);
            }
            else
            {
                query = query.Where(u => u.Username.ToLower().Contains(term)
                                      || (u.Email != null && u.Email.ToLower().Contains(term))
                                      || (u.Phone != null && u.Phone.Contains(term)));
            }
        }

        if (request.StatusFilter == "active")
        {
            query = query.Where(u => !u.IsDeleted);
        }
        else if (request.StatusFilter == "inactive")
        {
            query = query.Where(u => u.IsDeleted);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedDt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Username,
                u.Email ?? string.Empty,
                u.Phone,
                u.FirstName,
                u.LastName,
                u.IsDeleted,
                u.CreatedDt,
                u.Metadata))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

public record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public class GetUserByIdHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new UserDto(
                u.Id,
                u.Username,
                u.Email ?? string.Empty,
                u.Phone,
                u.FirstName,
                u.LastName,
                u.IsDeleted,
                u.CreatedDt,
                u.Metadata))
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {request.Id} not found");
        }

        return user;
    }
}