namespace StokCari.Core.Enums;

// Enum değerleri veritabanında metin olarak saklanır (HasConversion<string>), 0 bilinçli olarak boş bırakılmıştır:
// istekte alan gönderilmezse varsayılan 0 olur ve EnumDataType doğrulaması bunu reddeder.
public enum UserRole
{
    Admin = 1,
    User = 2
}
