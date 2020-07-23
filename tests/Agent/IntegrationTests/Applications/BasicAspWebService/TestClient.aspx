<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="TestClient.aspx.cs" Inherits="BasicAspWebService.TestClient" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">

    <head id="Head1" runat="server">

    </head>
    <body>
        <form id="Form1" runat="server">
            <asp:ScriptManager runat="server" ID="scriptManager">
                <Services>
                    <asp:ServiceReference path="~/HelloWorld.asmx" />
                </Services>
                <Scripts>
                    <asp:ScriptReference Path="~/TestClient.js" />
                </Scripts>
            </asp:ScriptManager>
        </form>

        <hr/>

        <div>
            <span id="Results"></span>
        </div>   

    </body>

</html>
