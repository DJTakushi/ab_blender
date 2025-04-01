- [1. functionality](#1-functionality)
  - [1.1 functionality](#11-functionality)
  - [1.2 structure](#12-structure)
- [2. use](#2-use)
  - [2.1 environment vars](#21-environment-vars)
  - [2.2 local](#22-local)
  - [2.3 containerization](#23-containerization)
      - [2.3.1 build](#231-build)
      - [2.3.2 run](#232-run)
- [3. requirements](#3-requirements)
- [4. developer notes](#4-developer-notes)

# 1. functionality
## 1.1 functionality
``` mermaid
flowchart TD
    tags.json>tags.json]
    style tags.json fill:#0000FF,color:#fff

    plc[/PLC/]
    style plc fill:#ff0000,color:#fff

    console[[console]]
    style console fill:#000000,color:#fff

    env_var{{env vars}}
    style rmq fill:#ff6600,stroke:#b8b8b8,stroke-width:2px,color:#fff
    style env_var fill:green,color:#fff

    subgraph plc_blender
        tags_
        tags_ --> is_rmq{rmq<br>connected?}
    end

    tags.json -.-> tags_
    env_var --> plc_blender
    plc <-- plc protocol --> tags_
    is_rmq  -- yes --> rmq[(rmq)]
    is_rmq -- no --> console
```

## 1.2 structure
```mermaid
---
title: class diagram
---
classDiagram
BackgroundService <|-- PlcBlender

class PlcBlender {
  - ITagAttributeFactory _tagFactory
  - Queue[string] outputs
  + PlcBlender()
  # ExecuteAsync()
  - publishOutputToRabbitMQ()
} 
PlcBlender --* IRabbitMQConnectionManager  : _connectionManager
PlcBlender --* IPlcFinder : _plcFinder 
PlcBlender --o  PlcManager : Dictionary[string, PlcManager] _tag_managers

IPlcFinder <|-- PlcFinder
IRabbitMQConnectionManager <|-- RabbitMQConnectionManager
ITagAttributeFactory <|-- TagAttributeFactory

class ITagAttributeFactory {
  + CreateTagAttribute(TagInfo, string, PlcType, Protocol) ITagAttribute 
}
<<Interface>> ITagAttributeFactory
class TagAttributeFactory {
  + CreateTagAttribute(TagInfo, string, PlcType, Protocol) ITagAttribute
}

class IRabbitMQConnectionManager {
    + IsConfigurable() bool
    + IsOutputOpen() bool
    + SetupConnectionsAsync()
    + PublishOutputToRabbitMQ(string)
}
<<interface>> IRabbitMQConnectionManager
class RabbitMQConnectionManager {
  - ConnectionFactory _outputFactory
  - IConnection _outputConnection
  - IChannel _outputChannel
  + IsConfigurable() bool
  + IsOutputOpen() bool
  - CreateFactory(string) ConnectionFactory
  - CreateConnection(ConnectionFactory, string) IConnection
  + SetupConnectionsAsync()
  + PublishOutputToRabbitMQ(string)
}
class IPlcFinder {
  + FindPlc(string, string)
  + GetPlcIps() string[]
}
<<interface>> IPlcFinder

class PlcFinder {
  + static plc_ips string[]
  + GetPlcIps() string[]
  + FindPlc(string, string)
  - ScanIpRange(string, string)
  - ScanDevice(string)
  - CheckPlcPort(string, int, int) bool
}
class PlcManager {
  - string _plc_address
  - PlcType _plc_type
  - Protocol _plc_protocol
  + readAllTags()
  + load_tags()
  - LoadTagsFromJson(string, PlcType, Protocol)
  - identifyPlcTagsWithMapper(string, PlcType, Protocol)
  + genTagTelemetry() string
}
PlcManager --o ITagAttributeFactory : _TagFactory
PlcManager --o ITagAttribute : Dictionary[string,ITagAttribute] attributes
class ITagAttribute {
  + InitializeTag()
  + GetTagType() TagType
  + GetTagName() string
  + ReadTag()
  + GetDoubleTagValue(int) double
  + GetBoolTagValue(int) bool
  + GetSintTagValue(int) int
  + GetIntTagValue(int) int
  + GetDintTagValue(int) int
  + GetStringTagValue(int) string
}
<<interface>> ITagAttribute
ITagAttribute <|-- TagAttribute
class TagAttribute {
  + TagInfo TagInfo
  - Tag Tag
  + GetTagType() TagType
  + GetTagName() string
  + InitializeTag()
  + ReadTag()
  + GetDoubleTagValue(int) double
  + GetBoolTagValue(int) bool
  + GetSintTagValue(int) int
  + GetIntTagValue(int) int
  + GetDintTagValue(int) int
  + GetStringTagValue(int) string
}
```

# 2. use

## 2.1 environment vars
see *example.env*
```
PLC_IP=192.168.1.100
PLC_TYPE=ControlLogix
PLC_PROTOCOL=ab_eip
STUB_PLC=false
READ_TAGS_PERIOD_MS=1000
RABBITMQ_HOST=192.168.130.51
RABBITMQ_USER=guest
RABBITMQ_PASS=guest
RABBITMQ_CONNECTION_NAME=plc_blender_output
RABBITMQ_EXCHANGE=plc_data
RABBITMQ_ROUTING_KEY=tag_values
```

## 2.2 local
from root dir
```
dotnet run --project plc_blender
```

## 2.3 containerization
### 2.3.1 build
```
dotnet publish --arch x64 /t:PublishContainer
```

add to `tmvcontainer.azurecr.io` registry with tag `0.0.00000`
```
dotnet publish --arch x64 /t:PublishContainer -p ContainerRegistry=tmvcontainer.azurecr.io -p ContainerImageTag=0.0.00000
```

###  2.3.2 run
```
docker run --env-file .env plc_blender
```

# 3. requirements
Create a C# application using net8.0 that reads tags from an Allen Bradley CompactLogix 5380 over ethernet/IP using the libplctag library.  The tags will be defined in a "tags.json" file which will define the name, datatype, and path for each tag.  Read the tags at a periodic rate defined by the environment variable READ_TAGS_PERIOD_MS.

Print these tags the first time each is identified by the application.

If the RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASS, RABBITMQ_EXCHANGE, and RABBITMQ_ROUTING_KEY environment variables are set, publish the tags and values in a json format.  Add to this message the time-stamp at which the data was received and the application version.  If the RabbitMQ connection is broken, reconnect at a rate defined by the environment variable RABBITMQ_RECONNETION_PERIOD_MS.


# 4. developer notes
- libplctag repo : https://github.com/libplctag/libplctag.NET
- built with `dotnet new console -o query_console -n query_conole`
- https://www.nuget.org/packages/libplctag/
  - example of use