using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Domain;

namespace AlSsareea.Modules.Promotions.Infrastructure;

internal sealed class PromotionScopeAuthorizer(
    IMerchantCatalogScopeProvider merchantScopes,
    ICatalogPromotionScopeProvider catalogScopes) : IPromotionScopeAuthorizer
{
    public async Task<bool> CanManageAsync(
        PromotionScope scope,
        PromotionActor actor,
        CancellationToken cancellationToken)
    {
        if (scope.Type == PromotionScopeType.Global)
            return actor.IsPlatformOperator;
        if (scope.MerchantId is not Guid merchantId)
            return false;

        MerchantCatalogScope? access = await merchantScopes.GetScopeAsync(
            merchantId,
            actor.UserId,
            actor.IsPlatformOperator,
            cancellationToken);
        if (access is not { CanManageMerchant: true })
            return false;

        if (scope.Type == PromotionScopeType.Branch)
        {
            foreach (Guid branchId in scope.TargetIds)
            {
                if (access.RestrictedBranchId is Guid restrictedBranchId && restrictedBranchId != branchId ||
                    !await merchantScopes.IsOperationalBranchAsync(merchantId, branchId, cancellationToken))
                    return false;
            }
        }

        return scope.Type switch
        {
            PromotionScopeType.Product => await catalogScopes.ProductsBelongToMerchantAsync(
                merchantId,
                scope.TargetIds,
                cancellationToken),
            PromotionScopeType.Category => await catalogScopes.CategoriesBelongToMerchantAsync(
                merchantId,
                scope.TargetIds,
                cancellationToken),
            _ => true,
        };
    }
}
