using System.ComponentModel.DataAnnotations;
namespace MilkerTools.Misc;

//public class AllowedValuesAttribute : ValidationAttribute
//{
//    private readonly int[] _allowedValues;

//    public AllowedValuesAttribute(params int[] allowedValues)
//    {
//        _allowedValues = allowedValues;
//    }

//    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
//    {
//        if (value != null && _allowedValues.Contains((int)value))
//        {
//            return ValidationResult.Success!;
//        }

//        return new ValidationResult($"The field {validationContext.DisplayName} must be one of the following values: {string.Join(", ", _allowedValues)}.");
//    }
//}
