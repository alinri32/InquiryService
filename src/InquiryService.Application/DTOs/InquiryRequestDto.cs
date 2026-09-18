namespace InquiryService.Application.DTOs;

public record InquiryRequestDto(
    string IdentityIdentifier,
    string InquiryType,
    bool BypassCache = false)
{
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(IdentityIdentifier))
            return (false, "شناسه استعلام ( کد ملی / شناسه فرد) الزامی است.");

        if (IdentityIdentifier.Length > 10)
            return (false, "طول شناسه استعلام نمی‌تواند بیشتر از ۱۰ کاراکتر باشد.");

        if (string.IsNullOrWhiteSpace(InquiryType))
            return (false, "فیلد InquiryType الزامی است.");

        if (!IsValidIdentityIdentifier(IdentityIdentifier))
            return (false, "شناسه استعلام نامعتبر است.");

        if (InquiryType.Length > 20)
            return (false, "طول InquiryType نمی‌تواند بیشتر از ۲۰ کاراکتر باشد.");

        return (true, null);
    }

    private bool IsValidIdentityIdentifier(string IdentityIdentifier)
    {
        IdentityIdentifier = IdentityIdentifier.Trim();

        if(IdentityIdentifier.StartsWith("999"))
            return true;
        if(IdentityIdentifier.StartsWith("888"))
            return true;
        if(IdentityIdentifier.StartsWith("777"))
            return true;


        if (IdentityIdentifier.Length != 10)
            return false;

        foreach (char c in IdentityIdentifier)
        {
            if (c < '0' || c > '9')
                return false;
        }

        bool allSame = true;
        for (int i = 1; i < 10; i++)
        {
            if (IdentityIdentifier[i] != IdentityIdentifier[0])
            {
                allSame = false;
                break;
            }
        }
        if (allSame)
            return false;

        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            int digit = IdentityIdentifier[i] - '0';
            sum += digit * (10 - i);
        }

        int remainder = sum % 11;
        int checkDigit = IdentityIdentifier[9] - '0';

        if (remainder < 2)
            return checkDigit == remainder;
        else
            return checkDigit == (11 - remainder);
    }
}