using System;
using System.Windows.Forms;
using NUnit.Framework;
using Rhino.Mocks;
using Rhino.Mocks.Constraints;
using cdeLib;
using cdeWin;

namespace cdeWinTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class TestDisplayTreeFromRootPresenter
    {
        [Test]
        public void LoadData_action_displays_Tree_and_List()
        {
            var mockView = MockRepository.GenerateStub<IDisplayTreeFromRootForm>();
            const string testRootPath = @"C:\Testing";
            const string testDirName = @"TestDir";
            var rootEntry = new RootEntry { RootPath = testRootPath };
            var dirEntry1 = new DirEntry
                                {
                                    IsDirectory = true,
                                    Name = testDirName,
                                    Modified = new DateTime(2011, 04, 14, 21, 09, 04, DateTimeKind.Local)
                                };
            var dirEntry2 = new DirEntry
                                {
                                    IsDirectory = false,
                                    Name = "TestFile",
                                    Size = 3311,
                                    Modified = new DateTime(2011, 03, 13, 22, 10, 5, DateTimeKind.Local)
                                };
            rootEntry.Children.Add(dirEntry1);
            rootEntry.Children.Add(dirEntry2);
            rootEntry.SetInMemoryFields();

            var testPresenter = new DisplayTreeFromRootPresenter(mockView, rootEntry);

            testPresenter.LoadData();

            mockView.AssertWasCalled(x => x.TreeViewNodes 
                = Arg<TreeNode>.Matches(
                    new PredicateConstraint<TreeNode>(y =>
                            y.Text == testRootPath
                             && y.Nodes.Count == 1
                             && y.Nodes[0].Text == testDirName
                        )));
        }

        //public class PropertiesMatchConstraint : AbstractConstraint
        //{
        //    private readonly object _equal;

        //    public PropertiesMatchConstraint(object obj)
        //    {
        //        _equal = obj;
        //    }

        //    public override bool Eval(object obj)
        //    {
        //        if (obj == null)
        //        {
        //            return (_equal == null);
        //        }
        //        var equalType = _equal.GetType();
        //        var objType = obj.GetType();
        //        foreach (var property in equalType.GetProperties())
        //        {
        //            var otherProperty = objType.GetProperty(property.Name);
        //            if (otherProperty == null || property.GetValue(_equal, null) != otherProperty.GetValue(obj, null))
        //            {
        //                return false;
        //            }
        //        }
        //        return true;
        //    }

        //    public override string Message
        //    {
        //        get
        //        {
        //            string str = _equal == null ? "null" : _equal.ToString();
        //            return "equal to " + str;
        //        }
        //    }
        //}
    }
    // ReSharper restore InconsistentNaming}
}

//namespace Test.Fohjin.DDD.Scenarios.Opening_the_bank_application
//{
//    public class When_in_the_GUI_openeing_the_bank_application : PresenterTestFixture<ClientSearchFormPresenter>
//    {
//        private List<ClientReport> _clientReports;

//        protected override void SetupDependencies()
//        {
//            _clientReports = new List<ClientReport> { new ClientReport(Guid.NewGuid(), "Client Name") };
//            OnDependency<IReportingRepository>()
//                .Setup(x => x.GetByExample<ClientReport>(null))
//                .Returns(_clientReports);
//        }

//        protected override void When()
//        {
//            Presenter.Display();
//        }

//        [Then]
//        public void Then_show_dialog_will_be_called_on_the_view()
//        {
//            On<IClientSearchFormView>().VerifyThat.Method(x => x.ShowDialog()).WasCalled();
//        }

//        [Then]
//        public void Then_client_report_data_from_the_reporting_repository_is_being_loaded_into_the_view()
//        {
//            On<IClientSearchFormView>().VerifyThat.ValueIsSetFor(x => x.Clients = _clientReports);
//        }
//    }
//}
