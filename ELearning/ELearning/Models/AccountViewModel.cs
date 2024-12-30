using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;

namespace ELearning.Models
{
    public class AccountViewModel
    {
    }

    public class LoginViewModel
    { 
        [Required]
        public string UserName { get; set; } = "";
        [Required]
        public string Password { get; set; } = ""; 
        public bool IsVerified { get; set; } = false;
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string UserType { get; set; } = "";
        public int UserID { get; set; } = 0;
        public string DefaultImageName { get; set; } = "";
        public int ClassID { get; set; } = 0;
        public string ClassName { get; set; } = "";
        public int SectionID { get; set; } = 0;
        public string SectionName { get; set; } = "";
        public bool IsActive { get; set; } = false; 
        public int CountNotVeriedStudents { get; set; } = 0;
        public int CountNotVeriedTeachers { get; set; } = 0;
        public string ReturnURL { get; set; } = ""; 
        public string TeacherName { get; set; } = "";

        public bool IsLock { get; set; } = false;
        public DateTime DateLocked { get; set; } 
    }

    public class RegisterViewModel
    {
        [Required]
        public string FullName { get; set; } = "";
        [Required]
        [EmailAddress(ErrorMessage = "Email is not valid.")]
        public string Email { get; set; } = "";
        [Required]
        public string UserName { get; set; } = "";
        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", ErrorMessage = "Password must contain at least 8 characters, including uppercase, lowercase, number, and special character.")]
        public string Password { get; set; } = "";
        [Required(ErrorMessage = "Confirm Password is required.")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Passwords don't match. Please try again.")]
        public string ConfirmPassword { get; set; } = "";
        public bool IsVerified { get; set; } = false;
        public string UserType { get; set; } = "";
        [Required]
        [Display(Name = "Grade Level")]
        public int ClassID { get; set; } = 0;
        [Required]
        [Display(Name = "Teacher")]
        public int TeacherID { get; set; } = 0;
        [Required]
        [Display(Name = "Section")]
        public int SectionID { get; set; } = 0;

        public IEnumerable<SelectListItem> ClassList { get; set; }
    }

     
}