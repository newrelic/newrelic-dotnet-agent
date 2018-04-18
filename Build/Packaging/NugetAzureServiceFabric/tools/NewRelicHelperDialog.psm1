# Creates a dialog box with the specified title and label to allow text input.
function Open-InputDialog ([System.String]$title, [System.String]$msg, [System.String]$optMsg){
    [void] [System.Reflection.Assembly]::LoadWithPartialName("System.Drawing") 
    [void] [System.Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms") 

    $objForm = New-Object System.Windows.Forms.Form 
    $objForm.Text = $title
    $objForm.Size = New-Object System.Drawing.Size(640,220) 
    $objForm.StartPosition = "CenterScreen"
    $objForm.FormBorderStyle = "FixedDialog"

    $objForm.KeyPreview = $True
    $objForm.Add_KeyDown({if ($_.KeyCode -eq "Enter") 
        {$script:inputResult=$objTextBox.Text;$objForm.Close()}})
    $objForm.Add_KeyDown({if ($_.KeyCode -eq "Escape") 
        {$script:inputResult=$null;$objForm.Close()}})

    $objLabel = New-Object System.Windows.Forms.Label
    $objLabel.Location = New-Object System.Drawing.Size(10,20) 
    $objLabel.Size = New-Object System.Drawing.Size(600,20) 
    $objLabel.Text = $msg
    $objForm.Controls.Add($objLabel)

    $objTextBox = New-Object System.Windows.Forms.TextBox 
    $objTextBox.Location = New-Object System.Drawing.Size(10,45) 
    $objTextBox.Size = New-Object System.Drawing.Size(600,20) 
    $objForm.Controls.Add($objTextBox) 

    if($optMsg -ne ""){
        $optionalLabel = New-Object System.Windows.Forms.Label
        $optionalLabel.Location = New-Object System.Drawing.Size(10,80) 
        $optionalLabel.Size = New-Object System.Drawing.Size(600,20) 
        $optionalLabel.Text = $optMsg
        $objForm.Controls.Add($optionalLabel) 
    }

    $OKButton = New-Object System.Windows.Forms.Button
    $OKButton.Location = New-Object System.Drawing.Size(405,115)
    $OKButton.Size = New-Object System.Drawing.Size(100,40)
    $OKButton.Text = "OK"
    $OKButton.Add_Click({$script:inputResult=$objTextBox.Text;$objForm.Close()})
    $objForm.Controls.Add($OKButton)

    $CancelButton = New-Object System.Windows.Forms.Button
    $CancelButton.Location = New-Object System.Drawing.Size(510,115)
    $CancelButton.Size = New-Object System.Drawing.Size(100,40)
    $CancelButton.Text = "Cancel"
    $CancelButton.Add_Click({$script:inputResult=$null;$objForm.Close()})
    $objForm.Controls.Add($CancelButton)

    $objForm.Topmost = $True
    $objForm.Add_Shown({$objForm.Activate()})
    [void] $objForm.ShowDialog()
    $inputResult
}
