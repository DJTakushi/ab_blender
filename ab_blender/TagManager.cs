
using libplctag;
using libplctag.DataTypes;
using System.Text.Json.Nodes;

public enum tagType // TODO : confirm these values
{
    BOOL,
    SINT = 193, //  193, addr, 3, 1 
    INT = 195, // 195, adr, 3,2
    DINT = 196, // expressed by vars with "Dint" in their name
    REAL = 202, // confirmed in the field
    STRING = 33633
}
class tag_attribute
{
    public required Tag Tag { get; set; }
    public required TagInfo TagInfo { get; set; }
}

class TagManager
{
    private List<tag_attribute> attributes = []; // TODO : make this a dict so a client can write a tag value
    public void readAllTags()
    {
        foreach (var attr in attributes)
        {
            attr.Tag.Read();
        }
    }
    public void load_tags()
    {
        string? plc_address  = EnvVarHelper.GetPlcAddress();
        PlcType _plc_type = EnvVarHelper.GetPlcType();
        Protocol _plc_protocol = EnvVarHelper.GetPlcProtocol();
        LoadTagsFromJson(plc_address, _plc_type, _plc_protocol);
        if (attributes.Count > 0)
        {
            identifyPlcTagsWithMapper(plc_address, _plc_type, _plc_protocol);
        }
    }
    private void LoadTagsFromJson(string plc_address, PlcType plc_type, Protocol plc_protocol)
    {
        string tag_def_fp = EnvVarHelper.get_tag_def_filepath()!;
        if (string.IsNullOrEmpty(tag_def_fp))
        {
            Console.WriteLine($"tag definition not set; no tags will be configured");
            return;
        }
        string jsonContent = File.ReadAllText(tag_def_fp);
        var jsonObj = JsonNode.Parse(jsonContent);
        foreach (var data in jsonObj!.AsArray())
        {
            try
            {
                string name = data!["Name"]!.ToString();
                string path = data!["Path"]!.ToString(); // WARNING :  https://github.com/libplctag/libplctag/wiki/Tag-String-Attributes
                ushort type_t = 0;
                switch (data!["DataType"]!.ToString())
                {
                    case "BOOL":
                        type_t = (ushort)tagType.BOOL;
                        break;
                    case "INT":
                        type_t = (ushort)tagType.INT;
                        break;
                    case "DINT":
                        type_t = (ushort)tagType.DINT;
                        break;
                    case "REAL":
                        type_t = (ushort)tagType.REAL;
                        break;
                    case "STRING":
                        type_t = (ushort)tagType.STRING;
                        break;
                    default:
                        Console.WriteLine($"Unknown type : {data["data_type"]}");
                        break;
                }

                attributes.Add(new tag_attribute
                {
                    Tag = new Tag
                    {
                        Name = name,
                        Path = path,
                        Gateway = plc_address,
                        PlcType = plc_type,
                        Protocol = plc_protocol
                    },
                    TagInfo = new TagInfo
                    {
                        Name = name,
                        Type = type_t
                    }
                });
                // if (!_stub_plc)
                // {
                //     attributes.Last().Tag.Initialize();
                // }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up tag '{data!.ToJsonString()}' : {ex.Message}");
            }
        }
    }
    private void identifyPlcTagsWithMapper(string plc_address, PlcType plc_type, Protocol plc_protocol)
    { // NOTE : https://github.com/libplctag/libplctag.NET/issues/406
        try
        {
            var tag_infos = new Tag<TagInfoPlcMapper, TagInfo[]>()  // OBSOLETE
            {
                Gateway = plc_address,
                Path = "1,0",  // TODO ; consider looping through potential values
                PlcType = plc_type,
                Protocol = plc_protocol,
                Name = "@tags"
            };

            if (!EnvVarHelper.GetPlcStub())
            {
                tag_infos.Read();
                // _outputs.Enqueue($"{tag_infos.Value}");
                foreach (var tag_info in tag_infos.Value)
                {
                    attributes.Add(new tag_attribute
                    {
                        Tag = new Tag
                        {
                            Name = $"{tag_info.Name}",
                            Path = "1,0", // assuming default,
                            Gateway = plc_address,
                            PlcType = plc_type,
                            Protocol = plc_protocol
                        },
                        TagInfo = tag_info
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in identifyPlcTagsWithMapper: {ex.Message}");
        }
    }
    public string genTagTelemetry()
    {
        JsonNode data = new JsonObject
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["app_version"] = AppInfo._appVersion,
            ["tags"] = new JsonObject()
        };

        foreach (var attr in attributes)
        {
            try
            {
                switch (attr.TagInfo.Type)
                {
                    case (ushort)tagType.REAL:
                        data["tags"]![attr.TagInfo.Name] = attr.Tag.GetFloat32(0);
                        break;
                    // TODO :  re-add this
                    // case (ushort)tagType.BOOL:
                    //     data["tags"]![attr.TagInfo.Name] = attr.Tag.GetBit(0);
                    //     break;
                    // case (ushort)tagType.SINT:
                    //     data["tags"]![attr.TagInfo.Name] = attr.Tag.GetInt8(0);
                    //     break;
                    // case (ushort)tagType.INT:
                    //     data["tags"]![attr.TagInfo.Name] = attr.Tag.GetInt16(0);
                    //     break;
                    // case (ushort)tagType.DINT:
                    //     data["tags"]![attr.TagInfo.Name] = attr.Tag.GetInt32(0);
                    //     break;
                    // case (ushort)tagType.STRING:
                    //     data["tags"]![attr.TagInfo.Name] = attr.Tag.GetString(0);
                    //     break;
                    // default:
                    //     Console.WriteLine($"Unknown type : {attr.TagInfo.Type}");
                    //     break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadTags for tag '{attr.TagInfo.Name}', type '{attr.TagInfo.Type}' : {ex.Message}");
            }
        }
        return data.ToJsonString();
    }
}