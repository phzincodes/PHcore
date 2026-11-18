# PHcore no Windows

Base PHsys2 `0c7cbce`, solução `PHsystem.slnx` e WPF em `net10.0-windows`. O SDK segue `global.json` e a distribuição usa `win-x64`. As versões são fixadas nos arquivos de projeto, no padrão da base.

## Bibliotecas

| Biblioteca | Versão | Onde | Integração |
|---|---:|---|---|
| WPF UI | 4.3.0 | Desktop | Ícone do painel ao vivo no Início. Os controles e a paleta Red Eclipse usam os estilos de `Theme.xaml` e `ThemeManager`. |
| CommunityToolkit.Mvvm | 8.4.2 | Desktop | Notificações e comando de pausa no painel ao vivo. |
| LibreHardwareMonitorLib | 0.9.6 | Windows | Uma instância compartilhada entre Início, Hardware e coletores de CPU e GPU. Leituras indisponíveis permanecem nulas. |
| LiveCharts2 / SkiaSharpView | 2.0.5 | Desktop | Renderizador em memória, com saída em bitmap WPF e série real de até 60 segundos. |
| Serilog / File Sink | 4.4.0 / 7.0.0 | Diagnostics | Falhas de coleta e gravação no diretório local, sem mensagens brutas de exceções ou conteúdo coletado. Rotação diária e por 2 MiB, com até sete arquivos. |
| Microsoft.Data.Sqlite | 10.0.15 | LocalData | A mesma versão do provedor EF Core. Migração aditiva para ações de manutenção no banco existente. |
| CliWrap | 3.10.5 | Windows | No diagnóstico de rede, a opção de saída do ping usa `ping.exe` do Windows. Captura código, saída e erros com prazo e cancelamento. |
| FlaUI.UIA3 | 5.0.0 | UiTests | Teste de uma janela WPF com dados isolados, abertura do Início e seleção do Diagnóstico por UI Automation. |
| Velopack / vpk | 1.2.161 | Desktop / ferramenta local | Inicialização do pacote instalado e geração do instalador. Distribuição offline e integração de atualizações ainda desativada. |
| Snoop | 6.1.1 | Ferramenta externa | Instalação separada no Windows, via release oficial. |

O pacote `LiveChartsCore.SkiaSharpView.WPF` 2.0.5 levou a `NU1701` com SkiaSharp.Views.WPF e OpenTK legados no alvo atual. O ScottPlot.WPF 5.1.59 também referencia esse pacote do Skia. O host WPF usa `LiveChartsCore.SkiaSharpView` 2.0.5 com a API de renderização em memória. WinGet fica para uma futura central de aplicativos.

A RAM vem da mesma chamada do coletor existente e a CPU é medida entre duas amostras válidas. Disco mostra o espaço ocupado no volume do Windows. GPU mostra o adaptador amostrado. O painel para de consultar quando a janela é minimizada, quando o usuário sai do Início ou Hardware e quando pausa manualmente. Dados em falta aparecem como **Indisponível**. Alguns sensores dependem do hardware e das permissões do Windows.

O histórico mantém as análises em `%LOCALAPPDATA%\PHsystemNext\snapshots.db`. O registro de manutenção identifica ferramentas abertas e falhas ao abrir. A execução da ação externa é observada no Windows. Os logs ficam em `%LOCALAPPDATA%\PHsystemNext\logs`.

## Compilar e verificar no Windows

Abra o PowerShell na raiz da solução com o SDK .NET 10 indicado em `global.json`.

```powershell
dotnet restore PHsystem.slnx
dotnet build PHsystem.slnx -c Release --no-restore
dotnet test PHsystem.slnx -c Release --no-build
dotnet run --project src/PHsystem.Desktop -c Release --no-build
```

1. No **Início**, aguarde a segunda amostra e alterne CPU, GPU, RAM e Disco no gráfico. Teste **Pausar**, **Retomar** e a navegação para **Hardware**.
2. Em **Configurações**, abra **Aparência** e escolha uma cor de destaque. Volte ao Início para conferir o gráfico e teste a rolagem com a janela reduzida.
3. Em **Diagnóstico → Rede / testes**, use `127.0.0.1` com **Opções do teste → Usar o ping do Windows**. Confira código e resposta. Um segundo teste pode ser interrompido pelo botão **Interromper teste**.
4. Em **Diagnóstico → Ferramentas oficiais**, abra uma ferramenta. Veja o registro em **Histórico → Manutenção** e confirme que as análises anteriores continuam visíveis.
5. O log recebe entradas quando um coletor falha ou excede o prazo, ou a gravação do diagnóstico falha. A suíte unitária inclui uma falha controlada para validar a redação do log sem mexer nos dados reais.

Para testar somente o fluxo de navegação FlaUI em uma sessão gráfica do Windows, execute `dotnet test tests/PHsystem.UiTests -c Release --filter FullyQualifiedName~FlaUiNavigationTests`.

## Empacotar

```powershell
.\tools\BuildVelopack.ps1
```

O script publica um build Release autocontido e gera o `Setup.exe` e o SHA-256 em `artifacts/velopack/0.3.0-preview.2`. A alternativa Inno Setup permanece disponível em `tools/BuildInstaller.ps1`. A prévia do Velopack é gerada localmente sem assinatura ou serviço de atualizações. A instalação em máquina limpa deve ser verificada antes de distribuir uma release.

## Inspecionar layout com Snoop

1. Baixe e instale o `Snoop.6.1.1.signed.msi` da release oficial. [Snoop v6.1.1](https://github.com/snoopwpf/snoopwpf/releases/tag/v6.1.1)
2. Abra o PHsystem e o Snoop na mesma sessão do Windows. Use o alvo da janela do Snoop para selecionar **PHsystem Next**.
3. Na árvore visual, selecione o `OverviewView`, o `ScrollViewer` ou o card com defeito. Compare `ActualWidth`, `ActualHeight`, `Visibility`, `DataContext` e os brushes de tema. Mude a largura da janela e acompanhe o layout do `ResponsivePanel`.

## Referências

- [WPF UI](https://wpfui.lepo.co/documentation/getting-started.html) · [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2) · [LiveCharts2](https://github.com/Live-Charts/LiveCharts2) · [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6)
- [CliWrap](https://github.com/Tyrrrz/CliWrap) · [Serilog File](https://github.com/serilog/serilog-sinks-file) · [SQLite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.15)
- [FlaUI](https://github.com/FlaUI/FlaUI) · [Velopack](https://docs.velopack.io/packaging/cross-compiling)
