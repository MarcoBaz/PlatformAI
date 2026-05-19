using System;

namespace PlatformAI.Infrastructure.DTO;


public class UserDTO
{
    public Guid Id { get; set;}
    public string Name { get; set;}
     public string Surname { get; set;}

     public string Email { get; set;}
     public string MobilePhone { get; set;}
    public string? Avatar { get; set; }
    public string Status { get; set; }
}
    