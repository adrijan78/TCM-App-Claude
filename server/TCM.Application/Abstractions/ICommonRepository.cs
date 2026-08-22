using TCM.Application.Dtos.Common;
using TCM.Domain.Entities;

namespace TCM.Application.Abstractions;

/// <summary>
/// The aggregate queries behind the dashboard (SPEC section 6.2) and the shared lookups. These
/// are computed in SQL rather than by loading rows and counting them in memory.
/// </summary>
public interface ICommonRepository : IRepository<Belt>
{
    Task<IReadOnlyList<BeltDto>> GetBeltsAsync(CancellationToken ct = default);

    /// <summary>
    /// Club-wide numbers, optionally narrowed to a year and month so the dashboard's filters
    /// can update the cards (SPEC section 6.2).
    /// </summary>
    Task<ClubNumbersInfoDto> GetClubNumbersAsync(int? clubId, int? year, int? month, CancellationToken ct = default);
}
