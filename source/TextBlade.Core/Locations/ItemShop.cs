using System.Text;
using TextBlade.Core.Commands;
using TextBlade.Core.Commands.Shops;
using TextBlade.Core.IO;

namespace TextBlade.Core.Locations;

public class ItemShop : Location
{
    // Deserialized, hence public.
    // TODO: Possibly make this tuples of (item, cost) so users can override costs.
    public IEnumerable<string> Items { get; set; }
    private readonly Dictionary<string, int> _itemCosts = new();
    
    public ItemShop(string name, string description, IEnumerable<string> items, string? locationClass = null) : base(name, description, locationClass)
    {
        this.Items = items;

        if (Items == null || !Items.Any())
        {
            throw new ArgumentException("Shop has no items", nameof(Items));
        }

        foreach (var itemName in Items)
        {
            var value = ItemsData.GetItem(itemName).Value;
            _itemCosts[itemName] = value;
        }
    }

    public override string GetExtraDescription()
    {
        var message = new StringBuilder();
        message.AppendLine("Items for sale:");
        var i = 0;

        foreach (var item in Items)
        {
            i++;
            message.AppendLine($"   {i}: {item}: {_itemCosts[item]} gold");
        }

        message.AppendLine($"You have {SaveData.Current.Gold} gold.");
        return message.ToString();
    }

    public override string GetExtraMenuOptions()
    {
        return "Type B to buy something.";
    }

    public override ICommand GetCommandFor(string input)
    {
        input = input.ToLower();
        if (!input.StartsWith("b") || input == "buy")
        {
            return new DoNothingCommand();
        }

        return new BuyFromShopCommand(this.Items, _itemCosts);
    }
}
