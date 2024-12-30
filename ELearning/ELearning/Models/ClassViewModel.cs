using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ELearning.Models
{
    public class ClassViewModel
    {
        public IPagedList<ClassModel> Classes { get; set; }
        public List<TeacherModel> TeacherList { get; set; }
    }

    public class ClassModel
    {
        public int ID { get; set; } = 0;
        public string Name { get; set; } = ""; 
        public List<SectionModel> Sections { get; set; } 
    } 

    public class ClassEditViewModel
    {
        public int ID { get; set; } = 0;
        public string Name { get; set; } = "";
        // List of Sections
        public List<SectionEditViewModel> Sections { get; set; } = new List<SectionEditViewModel>();

        // List of all available teachers
        public List<TeacherEditViewModel> TeacherList { get; set; } = new List<TeacherEditViewModel>();
    }

    public class SectionEditViewModel
    {
        public int ID { get; set; } // Section ID (optional)
        public string Name { get; set; } // Section Name
        public int TeacherID { get; set; } // Teacher ID 
    }

    public class TeacherEditViewModel
    {
        public int ID { get; set; } // Teacher ID
        public string Name { get; set; } // Teacher Name
        public bool IsSelected { get; set; } // For checkbox rendering (if needed)
    }

}