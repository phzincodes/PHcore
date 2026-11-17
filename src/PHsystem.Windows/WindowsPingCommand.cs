using System.Net;
using System.Net.Sockets;
using System.Text;
using CliWrap;

namespace PHsystem.Windows;

public sealed record WindowsPingResult(int ExitCode, string Output, string Error);

public static class WindowsPingCommand
{
    public static async Task<WindowsPingResult> RunAsync(IPAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            throw new ArgumentException("Família de endereços não suportada.", nameof(address));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(18));
        var output = new StringBuilder();
        var error = new StringBuilder();
        var result = await Cli.Wrap(Path.Combine(Environment.SystemDirectory, "ping.exe"))
            .WithArguments([address.AddressFamily == AddressFamily.InterNetwork ? "-4" : "-6",
                "-n", "5", "-w", "2000", address.ToString()])
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => AppendLine(output, line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => AppendLine(error, line)))
            .ExecuteAsync(deadline.Token);
        return new(result.ExitCode, output.ToString().Trim(), error.ToString().Trim());
    }

    private static void AppendLine(StringBuilder destination, string line)
    {
        const int maxCharacters = 8192;
        var room = maxCharacters - destination.Length - Environment.NewLine.Length;
        if (room <= 0) return;
        destination.Append(line, 0, Math.Min(room, line.Length)).AppendLine();
    }
}
