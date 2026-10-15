using System;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static partial class Program
{
    static JsonSerializerOptions JsonOptions()
    {
        var result = new JsonSerializerOptions { IncludeFields = true };
        result.Converters.Add(new ItemStackConverter());
        return result;
    }
    // Unity serializes ItemStack fields directly. Its pre-existing (id,n) constructor
    // does not bind in System.Text.Json, so adapt only this check harness, not gameplay.
    sealed class ItemStackConverter : JsonConverter<ItemStack>
    {
        public override ItemStack Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var json = JsonDocument.ParseValue(ref reader);
            var item = json.RootElement;
            return new ItemStack(item.GetProperty("itemId").GetString(), item.GetProperty("count").GetInt32());
        }
        public override void Write(Utf8JsonWriter writer, ItemStack value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("itemId", value.itemId);
            writer.WriteNumber("count", value.count);
            writer.WriteEndObject();
        }
    }
}
