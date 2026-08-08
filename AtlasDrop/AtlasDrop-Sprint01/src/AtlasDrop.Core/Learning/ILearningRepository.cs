namespace AtlasDrop.Core.Learning;

public interface ILearningRepository
{
    Task AddAsync(
        LearningEvent learningEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningEvent>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        CancellationToken cancellationToken = default);
}
