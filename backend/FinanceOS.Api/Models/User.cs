// A user represents someone with an account in Finance OS.

namespace FinanceOS.Api.Models;

/*
Id: every user neds an unique identifier. We'll use Guid so IDs are globally unique
Email: the user's email address, which is used for login and communication
PasswordHash: we never store plain text passwords, so we store a hash of the password instead
CreatedAt: the date and time when the user account was created, which can be useful for auditing and tracking purposes
*/

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

