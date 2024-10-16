using PolyPlane.GameObjects;

namespace PolyPlane
{
    public partial class SelectObjectUI : Form
    {
        public GameObject SelectedObject = null;

        private readonly GameObjectManager _objs;

        public SelectObjectUI(GameObjectManager objs)
        {
            _objs = objs;
            InitializeComponent();

            InitBoxes();
        }

        private void InitBoxes()
        {
            ObjectTypeCombo.Items.Clear();
            ObjectTypeCombo.Items.Add("Planes");
            ObjectTypeCombo.Items.Add("Missiles");

            ObjectTypeCombo.SelectedIndex = 0;
        }

        private void ObjectTypeCombo_SelectedValueChanged(object sender, EventArgs e)
        {
            UpdateObjectList();
        }

        private void UpdateObjectList()
        {
            ObjectsListbox.Items.Clear();
            ObjectsListbox.ValueMember = nameof(ObjectEntry);
            ObjectsListbox.DisplayMember = nameof(ObjectEntry);

            if (ObjectTypeCombo.SelectedItem != null)
            {
                var value = ObjectTypeCombo.SelectedItem.ToString();

                if (value == "Planes")
                {
                    foreach (var plane in _objs.Planes)
                    {
                        ObjectsListbox.Items.Add(new ObjectEntry(plane, $"{plane.ID}  {plane.Position}"));
                    }
                }
                else if (value == "Missiles")
                {
                    foreach (var missile in _objs.Missiles)
                    {
                        ObjectsListbox.Items.Add(new ObjectEntry(missile, $"{missile.ID}  {missile.Position}"));
                    }
                }
            }
        }

        private void SelectObject()
        {
            var selectedItem = ObjectsListbox.SelectedItem;
            var obj = selectedItem as ObjectEntry;

            if (obj != null)
            {
                SelectedObject = obj.Ref;
                World.ViewPlaneID = SelectedObject.ID;
            }
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            SelectObject();

            this.DialogResult = DialogResult.OK;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void ObjectsListbox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            SelectObject();
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            UpdateObjectList();
        }

        private class ObjectEntry
        {
            public GameObject Ref;
            public string Text;

            public ObjectEntry(GameObject objRef, string text)
            {
                Ref = objRef;
                Text = text;
            }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
