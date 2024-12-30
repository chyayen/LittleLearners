using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ELearning.Models
{
    public class SectionViewModel
    {
        public IPagedList<SectionModel> Sections { get; set; }
    }

    public class SectionModel
    {
        public int ID { get; set; } = 0;
        public string Name { get; set; } = "";
        public int ClassID { get; set; } = 0;
        public int TeacherID { get; set; } = 0;
    }
}