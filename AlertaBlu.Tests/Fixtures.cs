namespace AlertaBlu.Tests;

/// <summary>
/// Trimmed captures of the real AlertaBLU payloads, kept verbatim in the parts that matter:
/// HTML entities, comma decimals, the duplicated CSS classes across sibling tables and the
/// run-together date in the last forecast block.
/// </summary>
internal static class Fixtures
{
    public const string TemperaturasJson = """
        [
          {"fonte_site": "defesacivil.blumenau.sc.gov.br", "fonte_nome": "AlertaBLU", "valor": 15.52734375, "horaLeitura": "2026-08-13T09:00:00Z"},
          {"fonte_site": "defesacivil.blumenau.sc.gov.br", "fonte_nome": "AlertaBLU", "valor": 16.0400390625, "horaLeitura": "2026-08-13T11:00:00Z"},
          {"fonte_site": "defesacivil.blumenau.sc.gov.br", "fonte_nome": "AlertaBLU", "valor": 15.6097412109375, "horaLeitura": "2026-08-13T10:00:00Z"}
        ]
        """;

    public const string OpenMeteoJson = """
        {"latitude":-26.88928,"longitude":-49.09091,"timezone":"America/Sao_Paulo",
         "current_units":{"relative_humidity_2m":"%","apparent_temperature":"°C"},
         "current":{"time":"2026-08-13T08:30","interval":900,"relative_humidity_2m":93,"apparent_temperature":16.9}}
        """;

    /// <summary>
    /// The "detalhada" page: today's extremes plus four forecast blocks covering five days.
    /// The last block's date div runs two dates together, exactly as the site emits them.
    /// </summary>
    public const string DetalhadaHtml = """
        <html><body>
          <ul>
            <li><p><span class="highlight">M&iacute;n: </span><span class="temp-min">14 &ordm;C</span><span class="highlight"> / M&aacute;x: </span><span class="temp-max">22&ordm;C</span></p></li>
          </ul>

          <h3>Previs&atilde;o do Tempo para os Pr&oacute;ximos 5 dias</h3>

          <div class="previsao-item">
            <div class="data_listprev">13/08/2026</div>
            <div class="descricao_listprev">
              <p style="text-align:justify">Nesta quinta-feira, ocorrem algumas aberturas de sol.&nbsp;M&aacute;xima entre 21 e 23&ordm;C. Vento fraco de oeste.</p>
            </div>
          </div>

          <div class="previsao-item">
            <div class="data_listprev">14/08/2026</div>
            <div class="descricao_listprev">
              <p>Na sexta-feira, o c&eacute;u permanece encoberto. M&iacute;nima entre 14 e 16&ordm;C e m&aacute;xima entre 18 e 20&ordm;C.</p>
            </div>
          </div>

          <div class="previsao-item">
            <div class="data_listprev">15/08/2026</div>
            <div class="descricao_listprev">
              <p>Tempo inst&aacute;vel ao longo do dia, sem indica&ccedil;&atilde;o de temperaturas.</p>
            </div>
          </div>

          <div class="previsao-item">
            <div class="data_listprev">16/08/2026e 17/08/2026</div>
            <div class="descricao_listprev">
              <p>No fim de semana, sol entre nuvens. M&iacute;nimas entre 17 e 18&ordm;C e m&aacute;xima entre 21 e 23&ordm;C.</p>
            </div>
          </div>
        </body></html>
        """;

    /// <summary>River level page; the trend is encoded only in the arrow CSS class.</summary>
    public const string RiverHtml = """
        <html><body>
        <table class="table table-condensed table-bordered item_tabela" id="river-level-table">
          <thead><tr><th>Hora da Leitura</th><th>N&iacute;vel (m)</th><th>Varia&ccedil;&atilde;o (m)</th></tr></thead>
          <tbody>
            <tr>
              <td class="text-center"><div style="background-color: #64ee64">13/08/2026 08:00</div></td>
              <td class="text-center"><div style="background-color: #64ee64">2,25</div></td>
              <td class="text-center"><div>
                <span class="glyphicon glyphicon-arrow-down green"><i class="fa fa-arrow-down" aria-hidden="true"></i></span> 0,04
              </div></td>
            </tr>
            <tr>
              <td class="text-center"><div>13/08/2026 07:00</div></td>
              <td class="text-center"><div>2,29</div></td>
              <td class="text-center"><div>
                <span class="glyphicon glyphicon-arrow-up red"><i class="fa fa-arrow-up"></i></span> 0,02
              </div></td>
            </tr>
          </tbody>
        </table>
        </body></html>
        """;

    public const string CotasHtml = """
        <html><body>
        <table id="tabela_cotas" class="table table-condensed table-bordered hide table-responsiva">
          <thead><tr><th>Logradouro</th><th>Bairro</th><th>Cota</th><th>Observa&ccedil;&atilde;o</th></tr></thead>
          <tbody>
            <tr><td class="street">Rua Sao Rafael</td><td class="text-center">Itoupava Norte</td><td data-dynatable-sorts="cheia" class="text-center">7,40</td><td>Final da rua (pega s&oacute; uma casa)</td></tr>
            <tr><td class="street">Rua Gustavo Persuhn</td><td class="text-center">Itoupava Seca</td><td class="text-center">21,00</td><td>Esquina, lado da empresa CREMER</td></tr>
            <tr><td class="street">Rua Sem Cota</td><td class="text-center">Centro</td><td class="text-center">n/d</td><td></td></tr>
            <tr><td class="street"></td><td class="text-center">Vazia</td><td class="text-center">1,00</td><td></td></tr>
            <tr><td colspan="2">linha malformada</td></tr>
          </tbody>
        </table>
        <table class="table table-condensed table-bordered">
          <thead><tr><th>Outra tabela</th></tr></thead>
          <tbody><tr><td class="street">Nao deve aparecer</td></tr></tbody>
        </table>
        </body></html>
        """;

    /// <summary>
    /// Dam page. Four tables share the same CSS classes; only the first carries the dam data,
    /// which is why the parser locates it by its "% da Capacidade" header.
    /// </summary>
    public const string BarragensHtml = """
        <html><body>
        <table class="table table-condensed table-bordered">
          <thead><tr><th>Esta&ccedil;&atilde;o</th><th class="text-center">Hora da Leitura</th><th class="text-center">% da Capacidade</th><th class="text-center">Comportas</th></tr></thead>
          <tbody>
            <tr>
              <td>Barragem Oeste Tai&oacute;</td>
              <td class="text-center">13/08/2026 08:00</td>
              <td class="text-center"><div class="progress"><div class="progress-bar bg-success" style="width:2.90%"></div></div><small>2,90%</small></td>
              <td>
                <div><span class="badge badge-success"><span>&#128994;</span> Abertas: 0</span></div>
                <div class="mt-1"><span class="badge badge-secondary"><span>&#9898;</span> Fechadas: 7</span></div>
              </td>
            </tr>
            <tr>
              <td>Barragem Sul Ituporanga</td>
              <td class="text-center">13/08/2026 08:00</td>
              <td class="text-center"><div class="progress"><div class="progress-bar" style="width:17.30%"></div></div><small>17,30%</small></td>
              <td>
                <div><span class="badge badge-success">Abertas: 0</span></div>
                <div class="mt-1"><span class="badge badge-secondary">Fechadas: 5</span></div>
              </td>
            </tr>
            <tr>
              <td>Jos&eacute; Boiteux</td>
              <td class="text-center">13/08/2026 08:00</td>
              <td class="text-center"><small>3,10%</small></td>
              <td>
                <div><span class="badge badge-success">Abertas: 1</span></div>
                <div class="mt-1"><span class="badge badge-secondary">Fechadas: 1</span></div>
              </td>
            </tr>
          </tbody>
        </table>

        <table class="table table-condensed table-bordered">
          <thead><tr><th>N&iacute;vel do Rio</th><th>2,25m</th></tr></thead>
          <tbody><tr><td>ignorar</td><td>ignorar</td></tr></tbody>
        </table>
        </body></html>
        """;
}
