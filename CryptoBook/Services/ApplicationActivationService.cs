using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    /// <summary>
    /// Coordinates command-line and shell activations for the single application
    /// instance. Requests are queued until the main window has finished loading and
    /// are then handled by one reader in arrival order.
    /// </summary>
    public sealed class ApplicationActivationService:
        IApplicationActivationService
    {
        internal const string ApplicationMutexName = "CryptoBook.Application";
        internal const string ActivationPipeName =
            "CryptoBook.Application.Activation";

        private static readonly HashSet<string> SupportedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".cbook",
                ".cbox"
            };

        private readonly Lazy<IWorkspaceFileOpenService> fileOpenService;
        private readonly IMessageService messageService;
        private readonly IWindowManager windowManager;
        private readonly Dispatcher dispatcher;
        private readonly string applicationMutexName;
        private readonly string activationPipeName;
        private readonly Channel<string[]> activationRequests =
            Channel.CreateUnbounded<string[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        private readonly TaskCompletionSource<Guid> mainWindowReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource shutdown = new();

        private Mutex? applicationMutex;
        private Task? listenerTask;
        private Task? processorTask;
        private int started;
        private int disposed;

        public ApplicationActivationService(
            Lazy<IWorkspaceFileOpenService> fileOpenService,
            IMessageService messageService,
            IWindowManager windowManager,
            Dispatcher dispatcher)
            : this(
                fileOpenService,
                messageService,
                windowManager,
                dispatcher,
                ApplicationMutexName,
                ActivationPipeName)
        {
        }

        internal ApplicationActivationService(
            Lazy<IWorkspaceFileOpenService> fileOpenService,
            IMessageService messageService,
            IWindowManager windowManager,
            Dispatcher dispatcher,
            string applicationMutexName,
            string activationPipeName)
        {
            this.fileOpenService = fileOpenService ??
                throw new ArgumentNullException(nameof(fileOpenService));
            this.messageService = messageService ??
                throw new ArgumentNullException(nameof(messageService));
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            this.dispatcher = dispatcher ??
                throw new ArgumentNullException(nameof(dispatcher));
            this.applicationMutexName = applicationMutexName;
            this.activationPipeName = activationPipeName;
        }

        public async Task<bool> StartAsync(
            IReadOnlyList<string> commandLineArguments,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref disposed) != 0,
                this);
            if(Interlocked.Exchange(ref started, 1) != 0)
                throw new InvalidOperationException(
                    "Application activation has already been started.");

            string[] arguments = commandLineArguments?.ToArray() ?? [];
            var mutex = new Mutex(
                initiallyOwned: true,
                applicationMutexName,
                out bool isPrimaryInstance);

            if(!isPrimaryInstance)
            {
                mutex.Dispose();
                await ForwardToPrimaryInstanceAsync(
                    arguments,
                    cancellationToken);
                return false;
            }

            applicationMutex = mutex;
            listenerTask = ListenForActivationAsync(shutdown.Token);
            processorTask = ProcessActivationRequestsAsync(shutdown.Token);
            await activationRequests.Writer.WriteAsync(
                arguments,
                cancellationToken);
            return true;
        }

        public void NotifyMainWindowReady(Guid mainWindowId)
        {
            if(mainWindowId == Guid.Empty)
                throw new ArgumentException(
                    "The main window identifier cannot be empty.",
                    nameof(mainWindowId));
            mainWindowReady.TrySetResult(mainWindowId);
        }

        public static bool TryNormalizePath(
            string? argument,
            out string normalizedPath)
        {
            normalizedPath = string.Empty;
            if(string.IsNullOrWhiteSpace(argument))
                return false;

            string candidate = argument.Trim();
            if(candidate.Length >= 2 &&
               candidate[0] == '"' &&
               candidate[^1] == '"')
            {
                candidate = candidate[1..^1].Trim();
            }

            if(candidate.Length == 0)
                return false;

            try
            {
                normalizedPath = Path.GetFullPath(candidate);
                return true;
            }
            catch(Exception exception) when(
                exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
            {
                return false;
            }
        }

        public static bool IsSupportedPath(string path) =>
            SupportedExtensions.Contains(Path.GetExtension(path));

        private async Task ListenForActivationAsync(
            CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(
                        activationPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous |
                        PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(cancellationToken);

                    using var reader = new StreamReader(
                        pipe,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        leaveOpen: true);
                    using var writer = new StreamWriter(
                        pipe,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        leaveOpen: true)
                    {
                        AutoFlush = true
                    };

                    string? payload = await reader.ReadLineAsync(
                        cancellationToken);
                    string[]? arguments = payload is null
                        ? null
                        : JsonSerializer.Deserialize<string[]>(payload);
                    if(arguments is null)
                        continue;

                    await activationRequests.Writer.WriteAsync(
                        arguments,
                        cancellationToken);
                    await writer.WriteLineAsync(
                        "OK".AsMemory(),
                        cancellationToken);
                }
                catch(OperationCanceledException)
                    when(cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch(IOException)
                {
                    await DelayAfterPipeFailureAsync(cancellationToken);
                }
                catch(JsonException)
                {
                    // Ignore malformed messages. The pipe is local to this user,
                    // and a new server is created for the next activation.
                }
            }
        }

        private async Task ForwardToPrimaryInstanceAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            await using var pipe = new NamedPipeClientStream(
                ".",
                activationPipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous |
                PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token);

            using var reader = new StreamReader(
                pipe,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            using var writer = new StreamWriter(
                pipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                leaveOpen: true)
            {
                AutoFlush = true
            };

            string payload = JsonSerializer.Serialize(arguments);
            await writer.WriteLineAsync(payload.AsMemory(), timeout.Token);
            string? acknowledgement = await reader.ReadLineAsync(timeout.Token);
            if(!string.Equals(
                acknowledgement,
                "OK",
                StringComparison.Ordinal))
            {
                throw new IOException(
                    "The primary CryptoBook instance did not accept the activation request.");
            }
        }

        private async Task ProcessActivationRequestsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await foreach(string[] arguments in activationRequests.Reader
                    .ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        Guid mainWindowId = await mainWindowReady.Task.WaitAsync(
                            cancellationToken);
                        await dispatcher.InvokeAsync(
                                () => ProcessOnUiThreadAsync(
                                    mainWindowId,
                                    arguments,
                                    cancellationToken),
                                DispatcherPriority.Normal,
                                cancellationToken)
                            .Task
                            .Unwrap();
                    }
                    catch(OperationCanceledException)
                        when(cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch(Exception exception)
                    {
                        // One bad activation must not stop later shell requests.
                        Debug.WriteLine(exception);
                    }
                }
            }
            catch(OperationCanceledException)
                when(cancellationToken.IsCancellationRequested)
            {
            }
        }

        private async Task ProcessOnUiThreadAsync(
            Guid mainWindowId,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            windowManager.ActivateWindow(mainWindowId);

            foreach(string argument in arguments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(!TryNormalizePath(argument, out string path))
                {
                    await ShowErrorAsync(LocalizationManager.Format(
                        "Activation.InvalidPath",
                        argument));
                    continue;
                }

                if(!IsSupportedPath(path))
                {
                    await ShowErrorAsync(LocalizationManager.Format(
                        "Activation.UnsupportedFile",
                        path));
                    continue;
                }

                if(!File.Exists(path))
                {
                    await ShowErrorAsync(LocalizationManager.Format(
                        "Activation.FileUnavailable",
                        path));
                    continue;
                }

                try
                {
                    WorkspaceFileOpenResult result = await fileOpenService.Value
                        .OpenAsync(path, cancellationToken);
                    if(!result.Success && !result.Cancelled)
                    {
                        string details = string.IsNullOrWhiteSpace(result.Error)
                            ? string.Empty
                            : Environment.NewLine + result.Error;
                        await ShowErrorAsync(LocalizationManager.Format(
                            "Activation.OpenFailed",
                            path,
                            details));
                    }
                }
                catch(OperationCanceledException)
                    when(cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch(Exception exception)
                {
                    await ShowErrorAsync(LocalizationManager.Format(
                        "Activation.OpenFailed",
                        path,
                        Environment.NewLine + exception.Message));
                }
            }

            windowManager.ActivateWindow(mainWindowId);
        }

        private Task<Guid> ShowErrorAsync(string message) =>
            messageService.ShowMessage(
                LocalizationManager.GetString("Activation.OpenErrorTitle"),
                message);

        private static async Task DelayAfterPipeFailureAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch(OperationCanceledException)
                when(cancellationToken.IsCancellationRequested)
            {
            }
        }

        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            shutdown.Cancel();
            activationRequests.Writer.TryComplete();

            if(applicationMutex is not null)
            {
                try
                {
                    applicationMutex.ReleaseMutex();
                }
                catch(ApplicationException)
                {
                }
                applicationMutex.Dispose();
            }

            shutdown.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
