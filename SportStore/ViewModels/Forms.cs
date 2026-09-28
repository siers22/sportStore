using System.ComponentModel.DataAnnotations;

namespace SportStore.ViewModels;

public class LoginForm
{
    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Проверьте email.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Укажите пароль.")]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}

public class RegisterForm
{
    [Required(ErrorMessage = "Укажите имя."), StringLength(80)]
    public string DisplayName { get; set; } = "";
    [Required(ErrorMessage = "Укажите email."), EmailAddress]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Укажите пароль."), MinLength(8)]
    public string Password { get; set; } = "";
    [Required(ErrorMessage = "Повторите пароль."), Compare(nameof(Password), ErrorMessage = "Пароли не совпадают.")]
    public string ConfirmPassword { get; set; } = "";
    [Required]
    public string Role { get; set; } = "Buyer";
    [StringLength(100)]
    public string? StoreName { get; set; }
}

public class ProductForm
{
    [Required(ErrorMessage = "Укажите название."), StringLength(140)]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Добавьте описание."), StringLength(2000)]
    public string Description { get; set; } = "";
    [Range(0.01, 9999999, ErrorMessage = "Цена должна быть больше нуля.")]
    public decimal Price { get; set; }
    [Range(0, 1000000, ErrorMessage = "Остаток не может быть отрицательным.")]
    public int Stock { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Выберите категорию.")]
    public int CategoryId { get; set; }
    public string Visual { get; set; } = "default";
    public bool IsPublished { get; set; } = true;
}

public class CheckoutForm
{
    [Required(ErrorMessage = "Укажите получателя."), StringLength(100)]
    public string RecipientName { get; set; } = "";
    [Required(ErrorMessage = "Укажите адрес доставки."), StringLength(200)]
    public string DeliveryAddress { get; set; } = "";
    [Required(ErrorMessage = "Укажите телефон."), StringLength(30)]
    [RegularExpression(@"^(?:\+7|8)[\s-]*(?:\(\d{3}\)|\d{3})[\s-]*\d{3}[\s-]*\d{2}[\s-]*\d{2}$",
        ErrorMessage = "Введите российский номер: +7 900 000-00-00 или 8 900 000-00-00.")]
    public string ContactPhone { get; set; } = "";
}

public class ComplaintForm
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }
    [Required(ErrorMessage = "Опишите проблему."), StringLength(1000), MinLength(10, ErrorMessage = "Напишите хотя бы 10 символов.")]
    public string Reason { get; set; } = "";
}
