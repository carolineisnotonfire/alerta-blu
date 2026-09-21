using AlertaBlu.Domain;
using AlertaBlu.Infrastructure;
using Xunit;

namespace AlertaBlu.Tests;

public class AlertaBluParserTests
{
    #region temperaturas.json

    [Fact]
    public void ParseLatestTemperature_Should_ReturnNewestReading_When_FeedIsOutOfOrder()
    {
        // Arrange
        var json = Fixtures.TemperaturasJson;

        // Act
        var reading = AlertaBluParser.ParseLatestTemperature(json);

        // Assert
        Assert.Equal(16.04, reading.ValueC, precision: 2);
    }

    [Fact]
    public void ParseLatestTemperature_Should_ConvertUtcToBlumenauLocalTime()
    {
        // Arrange
        var json = Fixtures.TemperaturasJson;

        // Act
        var reading = AlertaBluParser.ParseLatestTemperature(json);

        // Assert: 11:00Z is 08:00 in Blumenau, which is UTC-3 all year.
        Assert.Equal(TimeSpan.FromHours(-3), reading.TimeLocal.Offset);
        Assert.Equal(8, reading.TimeLocal.Hour);
        Assert.Equal(new DateTime(2026, 8, 13), reading.TimeLocal.Date);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    public void ParseLatestTemperature_Should_Throw_When_FeedHasNoReadings(string json)
    {
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseLatestTemperature(json));
    }

    #endregion

    #region Open-Meteo

    [Fact]
    public void ParseApparentConditions_Should_ReadFeelsLikeAndHumidity()
    {
        // Act
        var conditions = AlertaBluParser.ParseApparentConditions(Fixtures.OpenMeteoJson);

        // Assert
        Assert.Equal(16.9, conditions.FeelsLikeC!.Value, precision: 2);
        Assert.Equal(93, conditions.HumidityPercent);
    }

    [Fact]
    public void ParseApparentConditions_Should_Throw_When_CurrentBlockIsMissing()
    {
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseApparentConditions("""{"latitude":-26.9}"""));
    }

    [Fact]
    public void ParseDailyConditions_Should_ReadOneEntry_PerDate()
    {
        // Act
        var daily = AlertaBluParser.ParseDailyConditions(Fixtures.OpenMeteoJson);

        // Assert
        Assert.Equal(5, daily.Count);
        var thursday = daily.Single(d => d.Date == new DateOnly(2026, 8, 13));
        Assert.Equal(23.5, thursday.FeelsLikeC!.Value, precision: 2);
        Assert.Equal(70, thursday.HumidityPercent);
    }

    [Fact]
    public void ParseDailyConditions_Should_LeaveFeelsLikeNull_When_ValueIsMissingFromTheArray()
    {
        // Act: 15/08/2026's apparent_temperature_max entry is a JSON null.
        var daily = AlertaBluParser.ParseDailyConditions(Fixtures.OpenMeteoJson);

        // Assert
        var saturday = daily.Single(d => d.Date == new DateOnly(2026, 8, 15));
        Assert.Null(saturday.FeelsLikeC);
        Assert.Equal(80, saturday.HumidityPercent);
    }

    [Fact]
    public void ParseDailyConditions_Should_ReturnEmpty_When_DailyBlockIsAbsent()
    {
        // Act
        var daily = AlertaBluParser.ParseDailyConditions("""{"current":{"apparent_temperature":16.9}}""");

        // Assert
        Assert.Empty(daily);
    }

    #endregion

    #region nivel_oficial.json

    [Fact]
    public void ParseRiverThresholds_Should_BuildAscendingBands_FromConsecutiveLevels()
    {
        // Act
        var thresholds = AlertaBluParser.ParseRiverThresholds(Fixtures.NivelOficialJson);

        // Assert
        Assert.Equal(3, thresholds.Count);
        Assert.Equal("Normalidade", thresholds[0].Label);
        Assert.Equal(0d, thresholds[0].MinMeters);
        Assert.Equal(3.0, thresholds[0].MaxMeters);
        Assert.Equal("Alerta", thresholds[2].Label);
        Assert.Null(thresholds[2].MaxMeters);
    }

    [Fact]
    public void ParseRiverThresholds_Should_Throw_When_NoConditionsArePublished()
    {
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseRiverThresholds("""{"condicoes":[]}"""));
    }

    [Fact]
    public void HighlightCurrent_Should_FlagLowestBand_When_LevelIsBelowEveryPublishedBand()
    {
        // Arrange
        var thresholds = AlertaBluParser.ParseRiverThresholds(Fixtures.NivelOficialJson);

        // Act: a re-baselined or negative reading, below the lowest band's 0m start.
        var flagged = AlertaBluParser.HighlightCurrent(thresholds, level: -0.5);

        // Assert
        Assert.True(flagged[0].IsCurrent);
        Assert.All(flagged.Skip(1), b => Assert.False(b.IsCurrent));
    }

    [Fact]
    public void HighlightCurrent_Should_TreatUpperBound_AsExclusive()
    {
        // Arrange
        var thresholds = AlertaBluParser.ParseRiverThresholds(Fixtures.NivelOficialJson);

        // Act: exactly 3,0m is where "Atenção" starts, so it must not stay in "Normalidade".
        var flagged = AlertaBluParser.HighlightCurrent(thresholds, level: 3.0);

        // Assert
        Assert.False(flagged[0].IsCurrent);
        Assert.True(flagged[1].IsCurrent);
    }

    [Fact]
    public void HighlightCurrent_Should_FlagTopBand_When_LevelIsAboveEveryUpperBound()
    {
        // Arrange
        var thresholds = AlertaBluParser.ParseRiverThresholds(Fixtures.NivelOficialJson);

        // Act: the highest band ("Alerta") is open-ended.
        var flagged = AlertaBluParser.HighlightCurrent(thresholds, level: 10.0);

        // Assert
        Assert.True(flagged[2].IsCurrent);
        Assert.All(flagged.Take(2), b => Assert.False(b.IsCurrent));
    }

    [Fact]
    public void HighlightCurrent_Should_ReturnBandsUnchanged_When_ThereIsNoReadingToPlace()
    {
        // Arrange
        var thresholds = AlertaBluParser.ParseRiverThresholds(Fixtures.NivelOficialJson);

        // Act
        var flagged = AlertaBluParser.HighlightCurrent(thresholds, level: null);

        // Assert
        Assert.All(flagged, b => Assert.False(b.IsCurrent));
    }

    #endregion

    #region /p/detalhada

    [Fact]
    public void ParseDayExtremes_Should_ReadBothSpans_When_UnitsAreFormattedInconsistently()
    {
        // Arrange: the site emits "14 &ordm;C" with a space and "22&ordm;C" without one.

        // Act
        var extremes = AlertaBluParser.ParseDayExtremes(Fixtures.DetalhadaHtml);

        // Assert
        Assert.Equal(14d, extremes.MinC);
        Assert.Equal(22d, extremes.MaxC);
    }

    [Fact]
    public void ParseDayExtremes_Should_ReturnNulls_When_SpansAreAbsent()
    {
        // Act: a page that no longer carries the temperature spans must degrade, not throw.
        var extremes = AlertaBluParser.ParseDayExtremes("<html><body><p>sem dados</p></body></html>");

        // Assert
        Assert.Null(extremes.MinC);
        Assert.Null(extremes.MaxC);
    }

    [Fact]
    public void ParseForecast_Should_SplitCombinedDateBlock_Into_FiveDays()
    {
        // Act
        var forecast = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml);

        // Assert: four blocks, but the last one covers "16/08/2026e 17/08/2026".
        Assert.Equal(5, forecast.Count);
        Assert.Equal(
            [
                new DateOnly(2026, 8, 13),
                new DateOnly(2026, 8, 14),
                new DateOnly(2026, 8, 15),
                new DateOnly(2026, 8, 16),
                new DateOnly(2026, 8, 17),
            ],
            forecast.Select(f => f.Date));
    }

    [Fact]
    public void ParseForecast_Should_ShareDescriptionAcrossBothDates_Of_CombinedBlock()
    {
        // Act
        var forecast = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml);

        // Assert
        var saturday = forecast.Single(f => f.Date == new DateOnly(2026, 8, 16));
        var sunday = forecast.Single(f => f.Date == new DateOnly(2026, 8, 17));

        Assert.Equal(saturday.Description, sunday.Description);
        Assert.Contains("fim de semana", saturday.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseForecast_Should_DecodeHtmlEntities_And_CollapseWhitespace()
    {
        // Act
        var friday = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml)
            .Single(f => f.Date == new DateOnly(2026, 8, 14));

        // Assert: "c&eacute;u" must reach the UI as "céu", with no entity residue.
        Assert.Contains("céu", friday.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("&", friday.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", friday.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseForecast_Should_ExtractBothRanges_When_DescriptionCarriesThem()
    {
        // Act
        var friday = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml)
            .Single(f => f.Date == new DateOnly(2026, 8, 14));

        // Assert
        Assert.Equal(new TemperatureRange(14, 16), friday.Min);
        Assert.Equal(new TemperatureRange(18, 20), friday.Max);
    }

    [Fact]
    public void ParseForecast_Should_MatchPluralPhrasing_Of_Minima()
    {
        // Act: the site writes "Mínimas entre 17 e 18ºC" for the weekend block.
        var saturday = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml)
            .Single(f => f.Date == new DateOnly(2026, 8, 16));

        // Assert
        Assert.Equal(new TemperatureRange(17, 18), saturday.Min);
    }

    [Fact]
    public void ParseForecast_Should_OmitOnlyTheMissingBound_When_DescriptionHasOneOfThem()
    {
        // Act: Thursday's text quotes a maximum but no minimum.
        var thursday = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml)
            .Single(f => f.Date == new DateOnly(2026, 8, 13));

        // Assert
        Assert.Null(thursday.Min);
        Assert.Equal(new TemperatureRange(21, 23), thursday.Max);
    }

    [Fact]
    public void ParseForecast_Should_KeepDayWithDescription_When_NoTemperatureMatches()
    {
        // Act
        var saturday = AlertaBluParser.ParseForecast(Fixtures.DetalhadaHtml)
            .Single(f => f.Date == new DateOnly(2026, 8, 15));

        // Assert: a day the regexes cannot read still renders as date + excerpt.
        Assert.Null(saturday.Min);
        Assert.Null(saturday.Max);
        Assert.True(saturday.HasNoTemperatures);
        Assert.NotEmpty(saturday.Summary);
    }

    [Fact]
    public void ParseForecast_Should_Throw_When_PageHasNoForecastBlocks()
    {
        Assert.Throws<FormatException>(
            () => AlertaBluParser.ParseForecast("<html><body><p>manutenção</p></body></html>"));
    }

    #endregion

    #region /d/nivel-do-rio

    [Fact]
    public void ParseRiverLevel_Should_ReadFirstRow_As_LatestReading()
    {
        // Act
        var river = AlertaBluParser.ParseRiverLevel(Fixtures.RiverHtml);

        // Assert
        Assert.Equal(2.25, river.LevelMeters, precision: 2);
        Assert.Equal(new DateTime(2026, 8, 13, 8, 0, 0), river.ReadingTime);
    }

    [Fact]
    public void ParseRiverLevel_Should_DeriveTrend_From_ArrowCssClass()
    {
        // Act
        var river = AlertaBluParser.ParseRiverLevel(Fixtures.RiverHtml);

        // Assert
        Assert.Equal(RiverTrend.Falling, river.Trend);
        Assert.Equal(0.04, river.DeltaMeters!.Value, precision: 2);
    }

    [Fact]
    public void ParseRiverLevel_Should_ReportRising_When_ArrowPointsUp()
    {
        // Arrange
        var html = Fixtures.RiverHtml.Replace("fa-arrow-down", "fa-arrow-up")
                                     .Replace("glyphicon-arrow-down", "glyphicon-arrow-up");

        // Act
        var river = AlertaBluParser.ParseRiverLevel(html);

        // Assert
        Assert.Equal(RiverTrend.Rising, river.Trend);
    }

    [Fact]
    public void ParseRiverLevel_Should_FallBackToUnknownTrend_When_ArrowIsAbsent()
    {
        // Arrange
        const string html = """
            <table id="river-level-table"><tbody>
              <tr><td><div>13/08/2026 08:00</div></td><td><div>3,01</div></td><td><div></div></td></tr>
            </tbody></table>
            """;

        // Act
        var river = AlertaBluParser.ParseRiverLevel(html);

        // Assert
        Assert.Equal(RiverTrend.Unknown, river.Trend);
        Assert.Equal(3.01, river.LevelMeters, precision: 2);
        Assert.Null(river.DeltaMeters);
    }

    [Fact]
    public void ParseRiverLevel_Should_Throw_When_TableIsMissing()
    {
        Assert.Throws<FormatException>(
            () => AlertaBluParser.ParseRiverLevel("<html><body><p>fora do ar</p></body></html>"));
    }

    #endregion

    #region /p/cotas

    [Fact]
    public void ParseCotas_Should_MapAllColumns_Of_ValidRows()
    {
        // Act
        var cotas = AlertaBluParser.ParseCotas(Fixtures.CotasHtml);

        // Assert
        var first = cotas[0];
        Assert.Equal("Rua Sao Rafael", first.Logradouro);
        Assert.Equal("Itoupava Norte", first.Bairro);
        Assert.Equal(7.40, first.CotaMeters!.Value, precision: 2);
        Assert.Equal("Final da rua (pega só uma casa)", first.Observacao);
    }

    [Fact]
    public void ParseCotas_Should_ParseCommaDecimals_Not_AsThousands()
    {
        // Act
        var cota = AlertaBluParser.ParseCotas(Fixtures.CotasHtml)[1];

        // Assert: "21,00" is 21 metres, never 2100.
        Assert.Equal(21.00, cota.CotaMeters!.Value, precision: 2);
    }

    [Fact]
    public void ParseCotas_Should_SkipUnusableRows_But_KeepTheRest()
    {
        // Act
        var cotas = AlertaBluParser.ParseCotas(Fixtures.CotasHtml);

        // Assert: the blank-street row and the malformed 2-cell row are dropped.
        Assert.Equal(3, cotas.Count);
        Assert.DoesNotContain(cotas, c => c.Logradouro.Length == 0);
    }

    [Fact]
    public void ParseCotas_Should_KeepRow_When_CotaValueIsUnreadable()
    {
        // Act
        var cota = AlertaBluParser.ParseCotas(Fixtures.CotasHtml)
            .Single(c => c.Logradouro == "Rua Sem Cota");

        // Assert
        Assert.Null(cota.CotaMeters);
        Assert.Equal("--", cota.CotaDisplay);
    }

    [Fact]
    public void ParseCotas_Should_IgnoreOtherTablesOnThePage()
    {
        // Act
        var cotas = AlertaBluParser.ParseCotas(Fixtures.CotasHtml);

        // Assert
        Assert.DoesNotContain(cotas, c => c.Logradouro == "Nao deve aparecer");
    }

    [Fact]
    public void ParseCotas_Should_Throw_When_TableIsMissing()
    {
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseCotas("<html><body></body></html>"));
    }

    #endregion

    #region /d/barragens

    [Fact]
    public void ParseBarragens_Should_ReadEveryDam_Regardless_Of_HowManyThereAre()
    {
        // Act
        var barragens = AlertaBluParser.ParseBarragens(Fixtures.BarragensHtml);

        // Assert: the site grew from two dams to three; nothing may be hard-coded to a count.
        Assert.Equal(3, barragens.Count);
        Assert.Equal("Barragem Oeste Taió", barragens[0].Estacao);
        Assert.Equal("José Boiteux", barragens[2].Estacao);
    }

    [Fact]
    public void ParseBarragens_Should_ReadCapacityAndReadingTime()
    {
        // Act
        var barragem = AlertaBluParser.ParseBarragens(Fixtures.BarragensHtml)[1];

        // Assert
        Assert.Equal(17.30, barragem.CapacityPercent!.Value, precision: 2);
        Assert.Equal(new DateTime(2026, 8, 13, 8, 0, 0), barragem.ReadingTime);
        Assert.Equal(0.173, barragem.CapacityFraction, precision: 3);
    }

    [Fact]
    public void ParseBarragens_Should_ReadGateCounts_From_BadgeText()
    {
        // Act
        var barragens = AlertaBluParser.ParseBarragens(Fixtures.BarragensHtml);

        // Assert
        Assert.Equal(0, barragens[0].GatesOpen);
        Assert.Equal(7, barragens[0].GatesClosed);
        Assert.Equal(1, barragens[2].GatesOpen);
        Assert.Equal(1, barragens[2].GatesClosed);
    }

    [Fact]
    public void ParseBarragens_Should_SelectTableByHeader_Not_ByCssClass()
    {
        // Act
        var barragens = AlertaBluParser.ParseBarragens(Fixtures.BarragensHtml);

        // Assert: the sidebar table shares the same classes and must not leak in.
        Assert.DoesNotContain(barragens, b => b.Estacao == "ignorar");
    }

    [Fact]
    public void ParseBarragens_Should_Throw_When_TableIsMissing()
    {
        Assert.Throws<FormatException>(
            () => AlertaBluParser.ParseBarragens("<html><body><table><tr><td>x</td></tr></table></body></html>"));
    }

    #endregion

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parsers_Should_Throw_When_DocumentIsEmpty(string html)
    {
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseCotas(html));
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseRiverLevel(html));
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseBarragens(html));
        Assert.Throws<FormatException>(() => AlertaBluParser.ParseForecast(html));
    }
}
