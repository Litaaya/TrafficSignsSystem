using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using FluentValidation;
using FluentValidation.Results;

namespace TrafficSigns.Application.Features.Accounts.Commands;

public record ReactivateAccountCommand(Guid AccountId) : IRequest<Guid>;

public class ReactivateAccountHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<ReactivateAccountCommand, Guid>
{
    public async Task<Guid> Handle(ReactivateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!permissionService.IsAdmin())
            throw new UnauthorizedAccessException("Access denied");      

        var account = await db.Accounts.FindAsync([request.AccountId], cancellationToken);

        if (account == null)
            throw new KeyNotFoundException("Invalid Account");

        if (!account.IsDeleted)
        {
            var failure = new ValidationFailure(nameof(request.AccountId), "Account is already active");
            throw new ValidationException(new[] { failure });
        }

        account.IsDeleted = false;
        account.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}

