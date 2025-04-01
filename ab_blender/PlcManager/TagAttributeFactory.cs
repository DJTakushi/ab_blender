using libplctag;
using libplctag.DataTypes;
public class TagAttributeFactory : ITagAttributeFactory
{
    public ITagAttribute CreateTagAttribute(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol)
    {
        if (EnvVarHelper.GetPlcStub())
        {
            return new TagAttributeStub(tagInfo, address, plc_type, plc_protocol);
        }
        else
        {
            return new TagAttribute(tagInfo, address, plc_type, plc_protocol);
        }
    }
}