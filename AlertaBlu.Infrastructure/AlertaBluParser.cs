using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlertaBlu.Application;
using AlertaBlu.Domain;
using AlertaBlu.Domain.Common;
using HtmlAgilityPack;

namespace AlertaBlu.Infrastructure;

/// <summary>
/// Pure parsing of the AlertaBLU payloads. Deliberately free of <see cref="HttpClient"/> and of
/// any MAUI type so the scraping rules can be unit-tested against captured fixtures without a
/// network or a UI.
/// </summary>
/// <remarks>
/// Every method is tolerant by design: the upstream markup is a live third-party site, so a
/// missing cell or an unexpected phrasing yields a null/omitted field rather than an exception.
/// Only a wholly unusable document throws <see cref="FormatException"/>.
/// </remarks>
public static partial class AlertaBluParser
{
    #region Regexes

    [GeneratedRegex(@"\d{2}/\d{2}/\d{4}", RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"-?\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    /// <summary>Matches "Mínima entre 14 e 16ºC", "Mínimas entre 17 e 18ºC" and "Mínima de 14ºC".</summary>
    [GeneratedRegex(
        @"m[íi]nimas?\s*[:\-]?\s*(?:entre|de)?\s*(\d{1,2})(?:\s*(?:e|a)\s*(\d{1,2}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinimaRegex();

    /// <summary>Matches "Máxima entre 21 e 23ºC", "máximas entre 21 e 23ºC" and "Máxima de 23ºC".</summary>
    [GeneratedRegex(
        @"m[áa]ximas?\s*[:\-]?\s*(?:entre|de)?\s*(\d{1,2})(?:\s*(?:e|a)\s*(\d{1,2}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaximaRegex();

    [GeneratedRegex(@"abertas\s*:?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GatesOpenRegex();

    [GeneratedRegex(@"fechadas\s*:?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GatesClosedRegex();

    #endregion

    #region JSON endpoints

    /// <summary>
    /// Reads the most recent entry of <c>temperaturas.json</c>, an array of hourly readings for
    /// today. The newest entry is selected by timestamp rather than by position so that an
    /// out-of-order feed still yields the current temperature.
    /// </summary>
    /// <exception cref="FormatException">The payload contained no usable reading.</exception>
    public static TemperatureReading ParseLatestTemperature(string json)
    {
        var readings = JsonSerializer.Deserialize(json, AlertaBluJsonContext.Default.TemperaturaDtoArray);

        if (readings is null || readings.Length == 0)
        {
            throw new FormatException("A lista de temperaturas veio vazia.");
        }

        var latest = readings.MaxBy(static r => r.HoraLeitura)
            ?? throw new FormatException("Nao foi possivel identificar a leitura mais recente.");

        return new TemperatureReading(latest.Valor, latest.HoraLeitura.ToOffset(PtBr.UtcOffset));
    }

    /// <summary>Reads sensação térmica and humidity from the Open-Meteo "current" block.</summary>
    /// <exception cref="FormatException">The payload had no "current" block.</exception>
    public static ApparentConditions ParseApparentConditions(string json)
    {
        var response = JsonSerializer.Deserialize(json, AlertaBluJsonContext.Default.OpenMeteoResponse);

        if (response?.Current is null)
        {
            throw new FormatException("Resposta do Open-Meteo sem o bloco 'current'.");
        }

        return new ApparentConditions(
            response.Current.ApparentTemperature,
            response.Current.RelativeHumidity);
    }

    /// <summary>
    /// Reads the Open-Meteo "daily" block into one entry per date, so the forecast strip can show
    /// a sensação térmica and a humidity for days other than today.
    /// </summary>
    /// <remarks>
    /// The block is three parallel arrays indexed by position. A missing or short value array is
    /// tolerated (that day simply reports no value) because losing a decorative figure must not
    /// cost the whole forecast; an absent block yields an empty list rather than an exception, for
    /// the same reason.
    /// </remarks>
    public static IReadOnlyList<DailyConditions> ParseDailyConditions(string json)
    {
        var daily = JsonSerializer
            .Deserialize(json, AlertaBluJsonContext.Default.OpenMeteoResponse)?.Daily;

        if (daily?.Time is not { Length: > 0 } dates)
        {
            return [];
        }

        var conditions = new List<DailyConditions>(dates.Length);

        for (var i = 0; i < dates.Length; i++)
        {
            if (!DateOnly.TryParseExact(
                    dates[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            var humidity = ValueAt(daily.RelativeHumidityMean, i);

            conditions.Add(new DailyConditions(
                date,
                ValueAt(daily.ApparentTemperatureMax, i),
                humidity is { } percent ? (int)Math.Round(percent) : null));
        }

        return conditions;

        static double? ValueAt(double?[]? values, int index) =>
            values is not null && index < values.Length ? values[index] : null;
    }

    #endregion

    #region nivel_oficial.json

    /// <summary>
    /// Turns the official <c>condicoes</c> list into displayable level bands.
    /// </summary>
    /// <remarks>
    /// Each entry publishes only the level at which its condition starts, so a band runs from its
    /// own level up to the next one and the highest band is left open-ended. The list is sorted
    /// defensively even though the feed already arrives ascending.
    /// </remarks>
    /// <exception cref="FormatException">The payload carried no usable condition.</exception>
    public static IReadOnlyList<RiverThreshold> ParseRiverThresholds(string json)
    {
        var condicoes = JsonSerializer
            .Deserialize(json, AlertaBluJsonContext.Default.NivelOficialResponse)?.Condicoes;

        if (condicoes is null || condicoes.Length == 0)
        {
            throw new FormatException("Nenhuma condicao de nivel encontrada.");
        }

        var ordered = condicoes
            .Where(static c => !string.IsNullOrWhiteSpace(c.Condicao))
            .OrderBy(static c => c.Nivel)
            .ToArray();

        if (ordered.Length == 0)
        {
            throw new FormatException("Condicoes de nivel sem descricao.");
        }

        var thresholds = new RiverThreshold[ordered.Length];

        for (var i = 0; i < ordered.Length; i++)
        {
            thresholds[i] = new RiverThreshold
            {
                Label = ordered[i].Condicao!.Trim(),
                MinMeters = ordered[i].Nivel,
                MaxMeters = i + 1 < ordered.Length ? ordered[i + 1].Nivel : null,
            };
        }

        return thresholds;
    }

    #endregion

    #region /p/detalhada

    /// <summary>
    /// Extracts today's forecast min/max from the <c>temp-min</c> / <c>temp-max</c> spans.
    /// Either value is null when its span is missing or unparseable.
    /// </summary>
    public static DayExtremes ParseDayExtremes(string html)
    {
        var root = LoadHtml(html);

        return new DayExtremes(
            ExtractFirstNumber(root, "//span[contains(@class,'temp-min')]"),
            ExtractFirstNumber(root, "//span[contains(@class,'temp-max')]"));

        static double? ExtractFirstNumber(HtmlNode root, string xpath)
        {
            var node = root.SelectSingleNode(xpath);
            if (node is null)
            {
                return null;
            }

            var match = NumberRegex().Match(Normalize(node.InnerText));
            return match.Success && PtBr.TryParseDecimal(match.Value, out var value) ? value : null;
        }
    }

    /// <summary>
    /// Extracts the "Previsão do Tempo para os Próximos 5 dias" blocks.
    /// </summary>
    /// <remarks>
    /// The site publishes 5 days across only 4 blocks: the final block's date div carries two
    /// dates run together (e.g. <c>16/08/2026e 17/08/2026</c>). Each date found in a block
    /// becomes its own card sharing that block's description.
    /// </remarks>
    public static IReadOnlyList<DailyForecast> ParseForecast(string html)
    {
        var root = LoadHtml(html);
        var blocks = root.SelectNodes("//div[contains(@class,'previsao-item')]");

        if (blocks is null || blocks.Count == 0)
        {
            throw new FormatException("Nenhum bloco de previsao encontrado na pagina.");
        }

        var forecasts = new List<DailyForecast>(capacity: 6);

        foreach (var block in blocks)
        {
            var dateText = block.SelectSingleNode(".//*[contains(@class,'data_listprev')]")?.InnerText ?? string.Empty;
            var description = Normalize(block.SelectSingleNode(".//*[contains(@class,'descricao_listprev')]")?.InnerText);

            // Parsed once per block and shared by every date the block covers.
            var min = ParseRange(MinimaRegex(), description);
            var max = ParseRange(MaximaRegex(), description);

            foreach (Match dateMatch in DateRegex().Matches(dateText))
            {
                if (!PtBr.TryParseDate(dateMatch.Value, out var date))
                {
                    continue;
                }

                forecasts.Add(new DailyForecast
                {
                    Date = date,
                    Description = description,
                    Min = min,
                    Max = max,
                });
            }
        }

        if (forecasts.Count == 0)
        {
            throw new FormatException("Nenhuma data valida encontrada na previsao.");
        }

        return forecasts
            .GroupBy(static f => f.Date)
            .Select(static g => g.First())
            .OrderBy(static f => f.Date)
            .ToArray();
    }

    /// <summary>Pulls a temperature range out of free-form forecast prose; null when absent.</summary>
    private static TemperatureRange? ParseRange(Regex regex, string description)
    {
        var match = regex.Match(description);
        if (!match.Success || !PtBr.TryParseDecimal(match.Groups[1].Value, out var low))
        {
            return null;
        }

        var high = match.Groups[2].Success && PtBr.TryParseDecimal(match.Groups[2].Value, out var parsedHigh)
            ? parsedHigh
            : low;

        // Guard against a reversed range in the source text.
        return new TemperatureRange(Math.Min(low, high), Math.Max(low, high));
    }

    #endregion

    #region /d/nivel-do-rio

    /// <summary>
    /// Reads the newest row of <c>#river-level-table</c>: timestamp, level in metres and the
    /// direction of change, which is encoded only as an <c>arrow-up</c>/<c>arrow-down</c> CSS
    /// class on an icon element.
    /// </summary>
    /// <exception cref="FormatException">The table or its first data row was missing.</exception>
    public static RiverLevel ParseRiverLevel(string html)
    {
        var root = LoadHtml(html);

        var row = root.SelectSingleNode("//table[@id='river-level-table']//tbody/tr")
            ?? throw new FormatException("Tabela de nivel do rio nao encontrada.");

        var cells = row.SelectNodes("./td");
        if (cells is null || cells.Count < 2)
        {
            throw new FormatException("Linha de nivel do rio com formato inesperado.");
        }

        if (!PtBr.TryParseDecimal(Normalize(cells[1].InnerText), out var level))
        {
            throw new FormatException("Nivel do rio ilegivel.");
        }

        DateTime? readingTime = PtBr.TryParseDateTime(Normalize(cells[0].InnerText), out var parsedTime)
            ? parsedTime
            : null;

        var trend = RiverTrend.Unknown;
        double? delta = null;

        if (cells.Count > 2)
        {
            var trendCell = cells[2];
            var classes = string.Join(
                ' ',
                trendCell.Descendants()
                    .Select(static n => n.GetAttributeValue("class", string.Empty)));

            trend = classes.Contains("arrow-up", StringComparison.OrdinalIgnoreCase) ? RiverTrend.Rising
                : classes.Contains("arrow-down", StringComparison.OrdinalIgnoreCase) ? RiverTrend.Falling
                : RiverTrend.Unknown;

            var deltaMatch = NumberRegex().Match(Normalize(trendCell.InnerText));
            if (deltaMatch.Success && PtBr.TryParseDecimal(deltaMatch.Value, out var parsedDelta))
            {
                delta = Math.Abs(parsedDelta);
            }
        }

        return new RiverLevel
        {
            LevelMeters = level,
            ReadingTime = readingTime,
            Trend = trend,
            DeltaMeters = delta,
        };
    }

    #endregion

    #region /p/cotas

    /// <summary>
    /// Reads every row of <c>#tabela_cotas</c>. Only that table is walked: the page also carries
    /// three unrelated sidebar tables and weighs ~700 KB, almost all of it this one table.
    /// </summary>
    /// <exception cref="FormatException">The table was missing or held no usable row.</exception>
    public static IReadOnlyList<CotaEnchente> ParseCotas(string html)
    {
        var root = LoadHtml(html);

        var rows = root.SelectNodes("//table[@id='tabela_cotas']//tbody/tr")
            ?? throw new FormatException("Tabela de cotas nao encontrada.");

        var cotas = new List<CotaEnchente>(rows.Count);

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count < 3)
            {
                continue;
            }

            var logradouro = Normalize(cells[0].InnerText);
            if (logradouro.Length == 0)
            {
                continue;
            }

            cotas.Add(new CotaEnchente
            {
                Logradouro = logradouro,
                Bairro = Normalize(cells[1].InnerText),
                CotaMeters = PtBr.TryParseDecimal(Normalize(cells[2].InnerText), out var cota) ? cota : null,
                Observacao = cells.Count > 3 ? Normalize(cells[3].InnerText) : string.Empty,
            });
        }

        if (cotas.Count == 0)
        {
            throw new FormatException("Tabela de cotas sem linhas legiveis.");
        }

        return cotas;
    }

    #endregion

    #region /d/barragens

    /// <summary>
    /// Reads the dam table. The page has four tables sharing the same CSS classes (the other
    /// three are sidebar widgets), so the table is located by its "% da Capacidade" header
    /// rather than by position or class.
    /// </summary>
    /// <exception cref="FormatException">The dam table was missing or held no usable row.</exception>
    public static IReadOnlyList<Barragem> ParseBarragens(string html)
    {
        var root = LoadHtml(html);

        var table =
            root.SelectSingleNode("//table[.//th[contains(., 'Capacidade')]]")
            ?? root.SelectSingleNode("//table[.//th[contains(., 'Comportas')]]")
            ?? throw new FormatException("Tabela de barragens nao encontrada.");

        var rows = table.SelectNodes(".//tbody/tr")
            ?? throw new FormatException("Tabela de barragens sem linhas.");

        var barragens = new List<Barragem>(rows.Count);

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count < 2)
            {
                continue;
            }

            var estacao = Normalize(cells[0].InnerText);
            if (estacao.Length == 0)
            {
                continue;
            }

            double? capacity = null;
            if (cells.Count > 2)
            {
                var percentMatch = PercentRegex().Match(Normalize(cells[2].InnerText));
                if (percentMatch.Success && PtBr.TryParseDecimal(percentMatch.Groups[1].Value, out var parsed))
                {
                    capacity = parsed;
                }
            }

            int? gatesOpen = null;
            int? gatesClosed = null;
            if (cells.Count > 3)
            {
                var gatesText = Normalize(cells[3].InnerText);
                gatesOpen = MatchCount(GatesOpenRegex(), gatesText);
                gatesClosed = MatchCount(GatesClosedRegex(), gatesText);
            }

            barragens.Add(new Barragem
            {
                Estacao = estacao,
                ReadingTime = PtBr.TryParseDateTime(Normalize(cells[1].InnerText), out var time) ? time : null,
                CapacityPercent = capacity,
                GatesOpen = gatesOpen,
                GatesClosed = gatesClosed,
            });
        }

        if (barragens.Count == 0)
        {
            throw new FormatException("Tabela de barragens sem linhas legiveis.");
        }

        return barragens;

        static int? MatchCount(Regex regex, string text)
        {
            var match = regex.Match(text);
            return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : null;
        }
    }

    #endregion

    #region Helpers

    private static HtmlNode LoadHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new FormatException("Documento HTML vazio.");
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document.DocumentNode;
    }

    /// <summary>
    /// Decodes HTML entities (the site emits <c>&amp;aacute;</c>, <c>&amp;ordm;</c>,
    /// <c>&amp;nbsp;</c> throughout) and collapses whitespace, so that scraped text is directly
    /// displayable and the temperature regexes see normal spaces.
    /// </summary>
    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var decoded = HtmlEntity.DeEntitize(raw) ?? raw;
        return WhitespaceRegex().Replace(decoded.Replace('\u00a0', ' '), " ").Trim();
    }

    #endregion
}
