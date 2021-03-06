﻿using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Storage;
using Content.Client.Interfaces.GameObjects;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Interfaces.GameObjects.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Players;

namespace Content.Client.GameObjects.Components.Storage
{
    /// <summary>
    /// Client version of item storage containers, contains a UI which displays stored entities and their size
    /// </summary>
    [RegisterComponent]
    public class ClientStorageComponent : SharedStorageComponent
    {
        private Dictionary<EntityUid, int> StoredEntities { get; set; } = new Dictionary<EntityUid, int>();
        private int StorageSizeUsed;
        private int StorageCapacityMax;
        private StorageWindow Window;

        public override void OnAdd()
        {
            base.OnAdd();

            Window = new StorageWindow()
                {StorageEntity = this};
        }

        public override void OnRemove()
        {
            Window.Dispose();
            base.OnRemove();
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel channel, ICommonSession session = null)
        {
            base.HandleNetworkMessage(message, channel, session);

            switch (message)
            {
                //Updates what we are storing for the UI
                case StorageHeldItemsMessage msg:
                    HandleStorageMessage(msg);
                    break;
                //Opens the UI
                case OpenStorageUIMessage _:
                    OpenUI();
                    break;
                case CloseStorageUIMessage _:
                    CloseUI();
                    break;
            }
        }

        /// <summary>
        /// Copies received values from server about contents of storage container
        /// </summary>
        /// <param name="storagestate"></param>
        private void HandleStorageMessage(StorageHeldItemsMessage storagestate)
        {
            StoredEntities = new Dictionary<EntityUid, int>(storagestate.StoredEntities);
            StorageSizeUsed = storagestate.StorageSizeUsed;
            StorageCapacityMax = storagestate.StorageSizeMax;
            Window.BuildEntityList();
        }

        /// <summary>
        /// Opens the storage UI
        /// </summary>
        private void OpenUI()
        {
            Window.Open();
        }

        private void CloseUI()
        {
            Window.Close();
        }

        /// <summary>
        /// Function for clicking one of the stored entity buttons in the UI, tells server to remove that entity
        /// </summary>
        /// <param name="entityuid"></param>
        private void Interact(EntityUid entityuid)
        {
            SendNetworkMessage(new RemoveEntityMessage(entityuid));
        }

        /// <summary>
        /// GUI class for client storage component
        /// </summary>
        private class StorageWindow : SS14Window
        {
            private Control VSplitContainer;
            private VBoxContainer EntityList;
            private Label Information;
            public ClientStorageComponent StorageEntity;

            private StyleBoxFlat _HoveredBox = new StyleBoxFlat {BackgroundColor = Color.Black.WithAlpha(0.35f)};
            private StyleBoxFlat  _unHoveredBox = new StyleBoxFlat {BackgroundColor = Color.Black.WithAlpha(0.0f)};

            protected override Vector2? CustomSize => (180, 320);

            public StorageWindow()
            {
                Title = "Storage Item";
                RectClipContent = true;

                var containerButton = new ContainerButton
                {
                    SizeFlagsHorizontal = SizeFlags.Fill,
                    SizeFlagsVertical = SizeFlags.Fill,
                    MouseFilter = MouseFilterMode.Pass,
                };

                var innerContainerButton = new PanelContainer
                {
                    PanelOverride = _unHoveredBox,
                    SizeFlagsHorizontal = SizeFlags.Fill,
                    SizeFlagsVertical = SizeFlags.Fill,
                };


                containerButton.AddChild(innerContainerButton);
                containerButton.OnPressed += args =>
                {
                    var controlledEntity = IoCManager.Resolve<IPlayerManager>().LocalPlayer.ControlledEntity;

                    if (controlledEntity.TryGetComponent(out IHandsComponent hands))
                    {
                        StorageEntity.SendNetworkMessage(new InsertEntityMessage());
                    }
                };

                VSplitContainer = new VBoxContainer()
                {
                    MouseFilter = MouseFilterMode.Ignore,
                };
                containerButton.AddChild(VSplitContainer);
                Information = new Label
                {
                    Text = "Items: 0 Volume: 0/0 Stuff",
                    SizeFlagsVertical = SizeFlags.ShrinkCenter
                };
                VSplitContainer.AddChild(Information);

                var listScrollContainer = new ScrollContainer
                {
                    SizeFlagsVertical = SizeFlags.FillExpand,
                    SizeFlagsHorizontal = SizeFlags.FillExpand,
                    HScrollEnabled = true,
                    VScrollEnabled = true,
                };
                EntityList = new VBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.FillExpand
                };
                listScrollContainer.AddChild(EntityList);
                VSplitContainer.AddChild(listScrollContainer);

                Contents.AddChild(containerButton);

                listScrollContainer.OnMouseEntered += args =>
                {
                    innerContainerButton.PanelOverride = _HoveredBox;
                };

                listScrollContainer.OnMouseExited += args =>
                {
                    innerContainerButton.PanelOverride = _unHoveredBox;
                };
            }

            public override void Close()
            {
                StorageEntity.SendNetworkMessage(new CloseStorageUIMessage());
                base.Close();
            }

            /// <summary>
            /// Loops through stored entities creating buttons for each, updates information labels
            /// </summary>
            public void BuildEntityList()
            {
                EntityList.DisposeAllChildren();

                var storagelist = StorageEntity.StoredEntities;

                foreach (var entityuid in storagelist)
                {
                    var entity = IoCManager.Resolve<IEntityManager>().GetEntity(entityuid.Key);

                    var button = new EntityButton()
                    {
                        EntityuID = entityuid.Key,
                        MouseFilter = MouseFilterMode.Stop,
                    };
                    button.ActualButton.OnToggled += OnItemButtonToggled;
                    //Name and Size labels set
                    button.EntityName.Text = entity.Name;
                    button.EntitySize.Text = string.Format("{0}", entityuid.Value);

                    //Gets entity sprite and assigns it to button texture
                    if (entity.TryGetComponent(out ISpriteComponent sprite))
                    {
                        button.EntitySpriteView.Sprite = sprite;
                    }

                    EntityList.AddChild(button);
                }

                //Sets information about entire storage container current capacity
                if (StorageEntity.StorageCapacityMax != 0)
                {
                    Information.Text = String.Format("Items: {0}, Stored: {1}/{2}", storagelist.Count,
                        StorageEntity.StorageSizeUsed, StorageEntity.StorageCapacityMax);
                }
                else
                {
                    Information.Text = String.Format("Items: {0}", storagelist.Count);
                }
            }

            /// <summary>
            /// Function assigned to button toggle which removes the entity from storage
            /// </summary>
            /// <param name="args"></param>
            private void OnItemButtonToggled(BaseButton.ButtonToggledEventArgs args)
            {
                var control = (EntityButton) args.Button.Parent;
                args.Button.Pressed = false;
                StorageEntity.Interact(control.EntityuID);
            }

            /// <summary>
            /// Function assigned to button that adds items to the storage entity.
            /// </summary>
            private void OnAddItemButtonPressed(BaseButton.ButtonEventArgs args)
            {
                var controlledEntity = IoCManager.Resolve<IPlayerManager>().LocalPlayer.ControlledEntity;

                if (controlledEntity.TryGetComponent(out IHandsComponent hands))
                {
                    StorageEntity.SendNetworkMessage(new InsertEntityMessage());
                }
            }
        }

        /// <summary>
        /// Button created for each entity that represents that item in the storage UI, with a texture, and name and size label
        /// </summary>
        private class EntityButton : PanelContainer
        {
            public EntityUid EntityuID { get; set; }
            public Button ActualButton { get; }
            public SpriteView EntitySpriteView { get; }
            public Control EntityControl { get; }
            public Label EntityName { get; }
            public Label EntitySize { get; }

            public EntityButton()
            {
                ActualButton = new Button
                {
                    SizeFlagsHorizontal = SizeFlags.FillExpand,
                    SizeFlagsVertical = SizeFlags.FillExpand,
                    ToggleMode = true,
                    MouseFilter = MouseFilterMode.Stop
                };
                AddChild(ActualButton);

                var hBoxContainer = new HBoxContainer();
                EntitySpriteView = new SpriteView
                {
                    CustomMinimumSize = new Vector2(32.0f, 32.0f)
                };
                EntityName = new Label
                {
                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    Text = "Backpack",
                };
                hBoxContainer.AddChild(EntitySpriteView);
                hBoxContainer.AddChild(EntityName);

                EntityControl = new Control
                {
                    SizeFlagsHorizontal = SizeFlags.FillExpand
                };
                EntitySize = new Label
                {
                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    Text = "Size 6",
                    Align = Label.AlignMode.Right,
                    /*AnchorLeft = 1.0f,
                    AnchorRight = 1.0f,
                    AnchorBottom = 0.5f,
                    AnchorTop = 0.5f,
                    MarginLeft = -38.0f,
                    MarginTop = -7.0f,
                    MarginRight = -5.0f,
                    MarginBottom = 7.0f*/
                };

                EntityControl.AddChild(EntitySize);
                hBoxContainer.AddChild(EntityControl);
                AddChild(hBoxContainer);
            }
        }
    }
}
