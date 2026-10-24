using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Users
{
    public sealed record AvatarInfo(string Key, string Name);

    public static class AvatarCatalog
    {
        public const string DefaultKey = "cat";

        public static readonly IReadOnlyList<AvatarInfo> All = new[]
        {
        new AvatarInfo("cat", "گربه"),       new AvatarInfo("fox", "روباه"),
        new AvatarInfo("owl", "جغد"),        new AvatarInfo("panda", "پاندا"),
        new AvatarInfo("bear", "خرس"),       new AvatarInfo("rabbit", "خرگوش"),
        new AvatarInfo("penguin", "پنگوئن"), new AvatarInfo("lion", "شیر"),
        new AvatarInfo("koala", "کوآلا"),    new AvatarInfo("frog", "قورباغه"),
        new AvatarInfo("tiger", "ببر"),      new AvatarInfo("unicorn", "تک‌شاخ"),
    };

        public static bool IsValid(string? key) => key is not null && All.Any(a => a.Key == key);

        public static string OrDefault(string? key) => IsValid(key) ? key! : DefaultKey;
    }
}
