using System.ComponentModel.DataAnnotations;

namespace SportStore.ViewModels;

public class LoginForm
{
    [Required(ErrorMessage = "Укажите логин или email."), StringLength(256)]
    public string Login { get; set; } = "";
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
    public IFormFile? Photo { get; set; }
    public bool RemovePhoto { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? ExistingImagePath { get; set; }
    public bool IsPublished { get; set; } = true;
}

public class CheckoutForm
{
    [Required(ErrorMessage = "Укажите получателя."), StringLength(100)]
    public string RecipientName { get; set; } = "";
    [Required(ErrorMessage = "Укажите адрес доставки."), StringLength(200)]
    public string DeliveryAddress { get; set; } = "";
    [Required(ErrorMessage = "Укажите телефон.")]
    [RegularExpression(@"^\+7 \([0-9]{3}\) [0-9]{3}-[0-9]{2}-[0-9]{2}$", ErrorMessage = "Введите номер целиком в формате +7 (900) 123-45-67.")]
    public string ContactPhone { get; set; } = "";
}

public class TopUpForm : IValidatableObject
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    [Range(typeof(decimal), "100", "1000000", ErrorMessage = "Сумма пополнения — от 100 до 1 000 000 ₽.")]
    public decimal Amount { get; set; } = 1000m;
    [Required(ErrorMessage = "Введите номер демонстрационной карты.")]
    [RegularExpression(@"^[0-9]{16}$", ErrorMessage = "В номере карты должно быть 16 цифр.")]
    public string CardNumber { get; set; } = "4242424242424242";
    [Required(ErrorMessage = "Укажите срок действия.")]
    [RegularExpression(@"^(0[1-9]|1[0-2])/[0-9]{2}$", ErrorMessage = "Введите срок в формате ММ/ГГ.")]
    public string Expiry { get; set; } = $"12/{DateTime.UtcNow.AddYears(3):yy}";
    [Required(ErrorMessage = "Введите CVC.")]
    [RegularExpression(@"^[0-9]{3}$", ErrorMessage = "CVC должен содержать 3 цифры.")]
    public string Cvc { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OperationId == Guid.Empty)
            yield return new ValidationResult("Обновите страницу и повторите пополнение.", [nameof(OperationId)]);
        if (decimal.Round(Amount, 2) != Amount)
            yield return new ValidationResult("Укажите сумму с точностью до копеек.", [nameof(Amount)]);
        if (Expiry is { Length: 5 } && int.TryParse(Expiry[..2], out var month) &&
            month is >= 1 and <= 12 && int.TryParse(Expiry[3..], out var year) &&
            new DateTime(2000 + year, month, 1).AddMonths(1) <= DateTime.UtcNow)
            yield return new ValidationResult("Срок действия карты истёк.", [nameof(Expiry)]);
    }
}

public class ComplaintForm
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }
    [Required(ErrorMessage = "Опишите проблему."), StringLength(1000), MinLength(10, ErrorMessage = "Напишите хотя бы 10 символов.")]
    public string Reason { get; set; } = "";
}
