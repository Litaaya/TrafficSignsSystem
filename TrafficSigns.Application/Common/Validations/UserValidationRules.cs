namespace TrafficSigns.Application.Common.Validations
{
    public static class UserValidationRules
    {
        public const int UsernameMax = 12;
        public static bool IsValidUsername(string? username) =>
            !string.IsNullOrWhiteSpace(username) && username.Length <= UsernameMax && !username.Contains(" ");
                
        public const int NameMax = 50;
        public static bool IsValidName(string? name) =>
            !string.IsNullOrWhiteSpace(name) && name.Length <= NameMax;

        public const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$";
        public static bool IsStrongPassword(string? password) =>
            !string.IsNullOrWhiteSpace(password) && System.Text.RegularExpressions.Regex.IsMatch(password, PasswordRegex);

        public static bool IsValidEmail(string? email) =>
            !string.IsNullOrWhiteSpace(email) && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);

        public const string PhoneRegex = @"^0[35789][0-9]{8}$";
        public static bool IsValidPhone(string? phone) =>
            !string.IsNullOrWhiteSpace(phone) && System.Text.RegularExpressions.Regex.IsMatch(phone, PhoneRegex);
    }
}
