  <px:PXSmartPanel runat="server" ID="pnlCopyItem" Height="360px" Width="720px" AllowResize="True" LoadOnDemand="True" AutoRepaint="True" CaptionVisible="True" Caption="Copy Item" Key="CopyDialog">
    <px:PXFormView runat="server" DefaultControlID="edInventoryCDNew" SkinID="Transparent" Width="100%" ID="frmCopyItem" DataMember="CopyDialog">
      <Template>
        <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="M" ControlSize="M" />
        <px:PXTextEdit runat="server" DataField="InventoryCDNew" ID="edInventoryCDNew" />
        <px:PXTextEdit runat="server" DataField="DescriptionNew" ID="edDescriptionNew" />
        <px:PXTextEdit runat="server" DataField="UsrIsbn13" ID="edUsrIsbn13" Text="ISBN 13" />
        <px:PXTextEdit runat="server" DataField="UsrIsbn10" ID="edUsrIsbn10" Text="ISBN 10" />
        <px:PXDateTimeEdit runat="server" ID="edUsrCopyrightDate" DataField="UsrCopyrightDate" />
        <px:PXCheckBox runat="server" DataField="CopyPrices" ID="chkCopyPrices" /></Template></px:PXFormView>
    <px:PXPanel runat="server" ID="pnlCopyItemButtons" ContentLayout-OuterSpacing="None" SkinID="Buttons">
      <px:PXButton runat="server" DialogResult="OK" ID="btnCopyItemOK" Text="OK" />
      <px:PXButton runat="server" DialogResult="Cancel" ID="btnCopyItemCancel" Text="Cancel" /></px:PXPanel></px:PXSmartPanel></asp:Content>