using FriendShip.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Questions
{
    public class Category
    {
        public const int NameMax = 40;
        public const int IconMax = 8;

        public int Id { get; private set; }
        public string Name { get; private set; } = default!;
        public string Icon { get; private set; } = default!;
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; }

        private Category() { }

        public static Category Create(string name, string icon, int sortOrder) => new()
        {
            Name = ValidName(name),
            Icon = ValidIcon(icon),
            SortOrder = sortOrder,
            IsActive = true
        };

        public void Update(string name, string icon, int sortOrder)
        {
            Name = ValidName(name);
            Icon = ValidIcon(icon);
            SortOrder = sortOrder;
        }

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;

        private static string ValidName(string? n) =>
            Guard.Text(n, 2, NameMax, "category.name", $"نام دسته باید بین ۲ تا {NameMax} حرف باشد.");

        private static string ValidIcon(string? i) =>
            Guard.Text(i, 1, IconMax, "category.icon", "آیکون دسته معتبر نیست.");
    }
}
