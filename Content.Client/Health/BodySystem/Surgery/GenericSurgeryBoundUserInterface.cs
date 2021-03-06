﻿using Content.Shared.Health.BodySystem.Surgery;
using Robust.Client.GameObjects.Components.UserInterface;
using Robust.Shared.GameObjects.Components.UserInterface;

namespace Content.Client.Health.BodySystem.Surgery
{

    //TODO : Make window close if target or surgery tool gets too far away from user.

    /// <summary>
    ///     Generic client-side UI list popup that allows users to choose from an option of limbs or organs to operate on.
    /// </summary>
    public class GenericSurgeryBoundUserInterface : BoundUserInterface
    {

        private GenericSurgeryWindow _window;

        public GenericSurgeryBoundUserInterface(ClientUserInterfaceComponent owner, object uiKey) : base(owner, uiKey)
        {

        }

        protected override void Open()
        {
            _window = new GenericSurgeryWindow();
            _window.OpenCentered();
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            switch (message)
            {
                case RequestBodyPartSurgeryUIMessage msg:
                    HandleBodyPartRequest(msg);
                    break;
                case RequestMechanismSurgeryUIMessage msg:
                    HandleMechanismRequest(msg);
                    break;
                case RequestBodyPartSlotSurgeryUIMessage msg:
                    HandleBodyPartSlotRequest(msg);
                    break;
            }
        }

        private void HandleBodyPartRequest(RequestBodyPartSurgeryUIMessage msg)
        {
            _window.BuildDisplay(msg.Targets, BodyPartSelectedCallback);
        }
        private void HandleMechanismRequest(RequestMechanismSurgeryUIMessage msg)
        {
            _window.BuildDisplay(msg.Targets, MechanismSelectedCallback);
        }
        private void HandleBodyPartSlotRequest(RequestBodyPartSlotSurgeryUIMessage msg)
        {
            _window.BuildDisplay(msg.Targets, BodyPartSlotSelectedCallback);
        }



        private void BodyPartSelectedCallback(int selectedOptionData)
        {
            SendMessage(new ReceiveBodyPartSurgeryUIMessage(selectedOptionData));
        }
        private void MechanismSelectedCallback(int selectedOptionData)
        {
            SendMessage(new ReceiveMechanismSurgeryUIMessage(selectedOptionData));
        }
        private void BodyPartSlotSelectedCallback(int selectedOptionData)
        {
            SendMessage(new ReceiveBodyPartSlotSurgeryUIMessage(selectedOptionData));
        }



        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;
            _window.Dispose();
        }
    }
}
