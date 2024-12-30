using PagedList;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ELearning.Models
{
    public class TeacherViewModel
    {
        public IPagedList<TeacherModel> Teachers { get; set; }
        public IEnumerable<SelectListItem> ClassList { get; set; }
    }

    public class TeacherModel
    {
        public int ID { get; set; } = 0;
        public string UserName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public int ClassID { get; set; } = 0;
        public string ClassName { get; set; } = "";
        public bool IsVerified { get; set; } = false;
        public string DefaultImageName { get; set; } = "";
        public HttpPostedFileBase ImageFile { get; set; }
        public int UpdatedBy { get; set; } = 0;
        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", ErrorMessage = "Password must contain at least 8 characters, including uppercase, lowercase, number, and special character.")]
        public string Password { get; set; } = "";
        [Required(ErrorMessage = "Confirm Password is required.")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Passwords don't match. Please try again.")]
        public string ConfirmPassword { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public bool IsLock { get; set; } = false;
        public List<int> ClassIDs { get; set; } // List of selected class IDs
        public List<ClassSectionSelection> ClassSectionSelections { get; set; } = new List<ClassSectionSelection>(); 

        public IEnumerable<SelectListItem> ClassList { get; set; } 

    }

    public class ClassSectionSelection
    {
        public int SectionID { get; set; } = 0;
        public string SectionName { get; set; } = "";
        public int ClassID { get; set; } = 0;
        public string ClassName { get; set; } = "";
        public List<SectionEditViewModel> Sections { get; set; } = new List<SectionEditViewModel>();
    }




}