# 1. use

## 1.1 environment vars
see *example.env*
```
PLC_IP=192.168.1.100
READ_TAGS_PERIOD_MS=1000
RABBITMQ_HOST=192.168.130.51
RABBITMQ_USER=guest
RABBITMQ_PASS=guest
RABBITMQ_EXCHANGE=plc_data
RABBITMQ_ROUTING_KEY=tag_values
RABBITMQ_RECONNECTION_PERIOD_MS=5000
```

## 1.2 local
from root dir
```
dotnet run --project ab_blender
```

## 1.3 containerization
### 1.3.1 build
```
dotnet publish --arch x64 /t:PublishContainer
```

###  1.3.2  run 
```
docker run --env-file .env ab_blender
```

# 2. functionality
## 2.1 functionality
:::mermaid
flowchart TD
    tags.json@{ shape: doc}
    style tags.json fill:#0000FF

    plc@{ shape : card, label: "Allen Bradley PLC"}
    style plc fill:#ff0000

    console@{ shape: div-rect}
    style console fill:#000000

    style rmq fill:#ff6600,stroke:#b8b8b8,stroke-width:2px,color:#fff
    style env_var fill:green

    subgraph ab_blender
        tags_
        tags_ --> is_rmq@{ shape: diamond, label: "rmq?"}
    end

    tags.json --> tags_
    env_var --> ab_blender
    plc <-- allen-bradley protocol --> tags_
    is_rmq  -- yes --> rmq[(rmq)]
    is_rmq -- no --> console

:::

## 2.2 structure
:::mermaid
---
title: Main
---
stateDiagram-v2

[*] --> LoadTagsFromJson()
state LoadTagsFromJson()  {
    state "_tags Deserialized from 'tags.json'" as tags
}

LoadTagsFromJson() --> InitializePlcTags()
state  InitializePlcTags() {
    state "loop through _tags, create real tags in _plcTags, and init them" as init
}   

state "_readTime setup" as _readTimer_setup  {
    state "_readTimer creation" as _readTimer
    state "_readTimer.Elapsed += ReadTags" as  set_readtags
    _readTimer --> set_readtags
}
InitializePlcTags() --> _readTimer_setup

_readTimer_setup --> HasRabbitMqConfig()
HasRabbitMqConfig() -->  _readTimer.Start() : false
HasRabbitMqConfig() --> rmq_reconnection_setup : true
state rmq_reconnection_setup{
    state "_reconnectTimer creation" as _reconnectTimer
    SetupRabbitMq() --> _reconnectTimer

    state "_reconnectTimer.Elapsed += ReconnectRabbitMq()" as reconnect_elapsed
    _reconnectTimer --> reconnect_elapsed
}
rmq_reconnection_setup -->  _readTimer.Start()

_readTimer.Start() --> loop

:::

:::mermaid
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
:::

# 3. requirements
Create a C# application using net8.0 that reads tags from an Allen Bradley CompactLogix 5380 over ethernet/IP using the libplctag library.  The tags will be defined in a "tags.json" file which will define the name, datatype, and path for each tag.  Read the tags at a periodic rate defined by the environment variable READ_TAGS_PERIOD_MS.

Print these tags the first time each is identified by the application.

If the RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASS, RABBITMQ_EXCHANGE, and RABBITMQ_ROUTING_KEY environment variables are set, publish the tags and values in a json format.  Add to this message the time-stamp at which the data was received and the application version.  If the RabbitMQ connection is broken, reconnect at a rate defined by the environment variable RABBITMQ_RECONNETION_PERIOD_MS.


# 4. developer notes
- libplctag repo : https://github.com/libplctag/libplctag.NET
- built with `dotnet new console -o query_console -n query_conole`
- https://www.nuget.org/packages/libplctag/
  - example of use