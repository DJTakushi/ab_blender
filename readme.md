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

    plc[/Allen Bradley PLC/]
    style plc fill:#ff0000,color:#fff

    console[[console]]
    style console fill:#000000,color:#fff

    env_var{{env vars}}
    style rmq fill:#ff6600,stroke:#b8b8b8,stroke-width:2px,color:#fff
    style env_var fill:green,color:#fff

    subgraph ab_blender
        tags_
        tags_ --> is_rmq{rmq?}
    end

    tags.json --> tags_
    env_var --> ab_blender
    plc <-- allen-bradley protocol --> tags_
    is_rmq  -- yes --> rmq[(rmq)]
    is_rmq -- no --> console
```

## 1.2 structure
```mermaid
---
title: Main
---
stateDiagram-v2

[*] --> LoadTagsFromJson()
state LoadTagsFromJson()  {
    state "_tags Deserialized <br>from 'tags.json'" as tags
}

LoadTagsFromJson() --> InitializePlcTags()
state  InitializePlcTags() {
    state "loop through _tags, create<br>real tags in _plcTags,<br> and init them" as init
}   

state "_readTime setup" as _readTimer_setup  {
    state "_readTimer creation" as _readTimer
    state "_readTimer.Elapsed<br>+= ReadTags" as  set_readtags
    _readTimer --> set_readtags
}
InitializePlcTags() --> _readTimer_setup

_readTimer_setup --> HasRabbitMqConfig()
HasRabbitMqConfig() -->  _readTimer.Start() : false
HasRabbitMqConfig() --> rmq_reconnection_setup : true
state rmq_reconnection_setup{
    state "_reconnectTimer creation" as _reconnectTimer
    SetupRabbitMq() --> _reconnectTimer

    state "_reconnectTimer.Elapsed<br>+= ReconnectRabbitMq()" as reconnect_elapsed
    _reconnectTimer --> reconnect_elapsed
}
rmq_reconnection_setup -->  _readTimer.Start()

_readTimer.Start() --> loop
```

```mermaid
---
title: ReadTags
---
stateDiagram-v2
state ReadTags {
  state loop_through_tags {
    update_tag
  }
  loop_through_tags --> jsonMessage
  state "jsonMessage creation" as jsonMessage

  state "message published to rmq" as rmq_publish
  jsonMessage --> rmq_publish :  rmq connected

  state "message printed to console" as console_print
  jsonMessage --> console_print :  rmq disconnected
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
RABBITMQ_CONNECTION_NAME=ab_blender
RABBITMQ_EXCHANGE=plc_data
RABBITMQ_ROUTING_KEY=tag_values
RABBITMQ_RECONNECTION_PERIOD_MS=5000
```

## 2.2 local
from root dir
```
dotnet run --project ab_blender
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
docker run --env-file .env ab_blender
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