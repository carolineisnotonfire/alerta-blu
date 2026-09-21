using AlertaBlu.Application;
using AlertaBlu.Domain;

namespace AlertaBlu.Tests;

/// <summary>
/// In-memory <see cref="IAlertaBluGateway"/> double: every property is independently settable and
/// defaults to a healthy value, so a test only needs to override the section it cares about.
/// </summary>
internal sealed class FakeAlertaBluGateway : IAlertaBluGateway
{
    public SectionResult<TemperatureReading> Temperature { get; set; } =
        SectionResult<TemperatureReading>.Ok(
            new TemperatureReading(16.04, new DateTimeOffset(2026, 8, 13, 8, 0, 0, TimeSpan.FromHours(-3))));

    public DetalhadaResult Detalhada { get; set; } = new(
        SectionResult<IReadOnlyList<DailyForecast>>.Ok(
            [new DailyForecast { Date = new DateOnly(2026, 8, 13) }]),
        new DayExtremes(14, 22));

    public OpenMeteoResult OpenMeteo { get; set; } = new(
        SectionResult<ApparentConditions>.Ok(new ApparentConditions(16.9, 93)),
        []);

    public SectionResult<RiverLevel> River { get; set; } =
        SectionResult<RiverLevel>.Ok(new RiverLevel { LevelMeters = 2.25 });

    public SectionResult<IReadOnlyList<RiverThreshold>> RiverThresholds { get; set; } =
        SectionResult<IReadOnlyList<RiverThreshold>>.Ok(
        [
            new RiverThreshold { Label = "Normalidade", MinMeters = 0, MaxMeters = 3.0 },
            new RiverThreshold { Label = "Atenção", MinMeters = 3.0, MaxMeters = 4.5 },
            new RiverThreshold { Label = "Alerta", MinMeters = 4.5 },
        ]);

    public SectionResult<IReadOnlyList<CotaEnchente>> Cotas { get; set; } =
        SectionResult<IReadOnlyList<CotaEnchente>>.Ok(
            [new CotaEnchente { Logradouro = "Rua Sao Rafael", CotaMeters = 7.40 }]);

    public SectionResult<IReadOnlyList<Barragem>> Barragens { get; set; } =
        SectionResult<IReadOnlyList<Barragem>>.Ok(
            [new Barragem { Estacao = "Barragem Oeste Taió", CapacityPercent = 2.90 }]);

    public int CotasCallCount { get; private set; }

    /// <summary>
    /// When set, <see cref="GetTemperatureAsync"/> waits on this task before returning, so a test
    /// can hold a <c>LoadAsync</c> call open to observe overlap/re-entrancy behaviour.
    /// </summary>
    public Task? TemperatureGate { get; set; }

    public async Task<SectionResult<TemperatureReading>> GetTemperatureAsync(CancellationToken cancellationToken = default)
    {
        if (TemperatureGate is not null)
        {
            await TemperatureGate.ConfigureAwait(false);
        }

        return Temperature;
    }

    public Task<DetalhadaResult> GetDetalhadaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Detalhada);

    public Task<OpenMeteoResult> GetOpenMeteoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OpenMeteo);

    public Task<SectionResult<RiverLevel>> GetRiverLevelAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(River);

    public Task<SectionResult<IReadOnlyList<RiverThreshold>>> GetRiverThresholdsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RiverThresholds);

    public Task<SectionResult<IReadOnlyList<Barragem>>> GetBarragensAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Barragens);

    public Task<SectionResult<IReadOnlyList<CotaEnchente>>> GetCotasAsync(CancellationToken cancellationToken = default)
    {
        CotasCallCount++;
        return Task.FromResult(Cotas);
    }
}
