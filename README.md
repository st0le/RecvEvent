# RecvEvent

Just a developer tool to receive events from Eventhub and print them to stdout.

# How to Compile

`dotnet publish -c Release`

# How to use

`RecvEvent.exe -n <namespace> -e <eventhub> -k <saskeyname> -v <saskeyvalue> [-b]`

See `RecvEvent --help` for more details.

# Why?

Dotnet Core rewrite of an earlier [golang app](https://github.com/st0le/eventhub-consumer-go)