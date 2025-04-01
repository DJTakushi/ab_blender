using libplctag;
using libplctag.DataTypes;
public interface ITagAttributeFactory
{
    public abstract ITagAttribute CreateTagAttribute(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol);
}