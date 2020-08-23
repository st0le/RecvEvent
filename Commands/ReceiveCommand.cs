using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using Microsoft.Azure.EventHubs;


namespace RecvEvent.Commands
{
    [Command]
    public class ReceiveCommand : ICommand
    {
        [CommandOption("namespace", 'n', IsRequired = true, Description = "Eventhub Namespace")]
        public string Namespace { get; set; }

        [CommandOption("eventhub", 'e', IsRequired = true, Description = "Eventhub Name")]
        public string EventHub { get; set; }

        [CommandOption("saskeyname", 'k', IsRequired = true, Description = "Eventhub SasKeyName")]
        public string SasKeyName { get; set; }

        [CommandOption("saskeyvalue", 'v', IsRequired = true, Description = "Eventhub SasKeyValue")]
        public string SasKeyValue { get; set; }

        [CommandOption("begin", 'b', Description = "Start reading from beginning")]
        public bool ShouldReadFromStart { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var tcs = new TaskCompletionSource<bool>();
            EventHubClient client = EventHubClient.CreateFromConnectionString($"Endpoint=sb://{Namespace}.servicebus.windows.net;EntityPath={EventHub};SharedAccessKeyName={SasKeyName};SharedAccessKey={SasKeyValue}");
            var runtimeInformation = await client.GetRuntimeInformationAsync();
            var partitionReceivers = runtimeInformation.PartitionIds.Select(partitionId => client.CreateReceiver("$Default", partitionId, ShouldReadFromStart ? EventPosition.FromStart() : EventPosition.FromEnd())).ToList();
            Channel<string> queue = Channel.CreateBounded<string>(10000);
            PartitionHandler pr = new PartitionHandler(queue);

            Task readerTask = Task.Run(async () =>
            {
                while (await queue.Reader.WaitToReadAsync())
                {
                    await foreach (string s in queue.Reader.ReadAllAsync())
                    {
                        await console.Output.WriteLineAsync(s);
                    }
                }
            });

            foreach (var partitionReceiver in partitionReceivers)
            {
                partitionReceiver.SetReceiveHandler(pr);
            }

            Console.CancelKeyPress += async (s, e) =>
            {
                await client.CloseAsync();
                queue.Writer.TryComplete();
                await readerTask;
                tcs.TrySetResult(true);
            };

            await tcs.Task;
        }


        private class PartitionHandler : IPartitionReceiveHandler
        {
            private readonly Channel<string> queue;

            public PartitionHandler(Channel<string> queue)
            {
                this.queue = queue;
            }
            public int MaxBatchSize { get; set; } = 1000;

            public Task ProcessErrorAsync(Exception error)
            {
                return Task.CompletedTask;
            }

            public async Task ProcessEventsAsync(IEnumerable<EventData> events)
            {
                foreach (var eventData in events)
                {
                    await this.queue.Writer.WriteAsync(Encoding.UTF8.GetString(eventData.Body));
                }
            }
        }
    }
}