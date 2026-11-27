---
name: rightrule_developer_app_build
description: Builds RightRule_Developer application. RightRule_Developer is a tool for developers to test rules in a simulated data environment.
---

## 1. Scope & Rules
RightRule_Developer is a Desktop Application written in C#.
It should host a service collection and offer services via Dependency Injection.
The RightRule library should be a package dependency.
It should be easy to install locally.

The application is intended to provide a developer or system admin a tool to create a data environment that simulates their production system data environment, so they can implement and test rules, and confirm the required decisions are being returned.
The tested rules can then be serialized to file, and copied to their production system ready to be loaded and used.

The start up option to load Custom Operators is set as true.
The default path to the custom operators is set as: Operators/CustomOperators/
The library of custom operators is temporarily in: "C:\Development\BSoft\RightRule_Developer\RightRule_Developer\CustomOperators"

The RightRule_Developer Application should start without a RightRule session loaded.
It should list existing serialized sessions that can be loaded one at a time, or start new session.
If a new session is started, it should prompt the user to confirm the session options.
Each session should create a folder to hold the serialized data and rules, and chosen options.
The folder name can be the name of the session.

## Menu
- File
New Session. Show dialog to create a new session. Provide data entry for Session Name and Description. These fields should be on a single modal dialog. OK and Cancel buttons
Load Session. Show dialog with dropdown of available sessions. Load and Cancel buttons
Remove Session. Show dialog with dropdown of available sessions. Remove and Cancel buttons. Remove should delete the session. 
Save Session data and rules to file. Include the filepath and filename in the "Activity and results" panel when saving a Session.
Exit. Prompt to save if dirty.

- Remove the "Load data and rules" option. This is covered by "Load Session".

- In the File menu, in a section above Exit, include menu item to be able to set/edit RightRuleOptions.
The options that are set will be applied to both a new session and a session that is to be loaded. The option settings are global to RightRule_Developer.
The configured options need to be saved, and then loaded when RightRule_Developer starts up. The options should be applied when a session is loaded or created.
The "Custom Operator configuration path", needs to include a button to locate the file. The associated text box needs to be big enough to contain a long file path string.
The file text box does not work well with horizontal scrolling. Make it a read only text box that is 5 lines deep.
Shrink the button and place it level with top of the text box, to the right.
When the File dialog opens, start it in the folder specified in the "Custom Operator configuration path". If that is empty start in the root of the session folders
The RightRule options dialog should show options in this order top to bottom:
Enable Data Checks
Enable Diagnostics
(Draw line across form to separate sections)
Load Default Operators
Default Operator names (one per line; blank means all)
(Draw line across form to separate sections)
Load Custom Operators
Custom Operator names (one per line; blank means all)
(Draw line across form to separate sections)
Custom Operator configuration path
Can you make the line heavier or increase white space above and below each separator line?

- Edit
Create and register new Dataset and associated data items. Assume simple data structure.
Add new data item to selected Dataset.
Create and register new Rule.
Create and register Default Rule
Edit Data (Deregister then register). (Allow user to change data values for selected "DatasetName")
Edit Rules (Deregister then register). (Allow user to change rule Decision, DecisionData, Statement, OptionalData for selected "Context")

- Export
Option to export Rules to file. This should include an option to export all Rules to a single file, or rules for each context to a file on their own, with the file name including the context name.
Sample Rule file can be found in: "C:\Development\BSoft\RightRule_Developer\RightRule_Developer\ExportSamples\RuleSample.json"
Option to export Data to file. This should include an option to export all Datasets to a single file, or data for each dataset to a file on their own, with the file name including the dataset name.
Sample Data file can be found in: "C:\Development\BSoft\RightRule_Developer\RightRule_Developer\ExportSamples\DataSample.json"

- Remove the Global Data menu.

- Help
Option to show list of loaded Operators and the aliases for each Operator.
Add "About RightRule Developer".
In "About RightRule Developer" include version of RightRule and RightRule.System.Common libraries.

- Function Ribbon at top of screen below menu bar (Left portion of ribbon)
Show name of loaded Session. Text should be bold.
Remove the list of loaded operators
Show list of registered datasets
List of registered contexts
Show list of registered rules per selected context. List with 2 columns, Name, Aliases, Default
The default column is ticked when default, empty when custom.

- Ribbon (Right portion of ribbon)
Drop down of rule application methods, single, First, Any, All.
Button to Apply rules per selected context, using selected application method.

When Data is loaded, the result of data loading should be shown on screen
When Rules are loaded, the results of rule loading should be shown on screen

# Form layout
Left - Rules List. Title should be in bold "Registered Rules" followed by " - " followed by selected Context. Include headers: Context, Number, Name, Decision, Statement. Double click row should open modal window to allow edit of rule, or deletion of rule. DecisionData can be JSON so you can add the "JSON" comment. Rules list should expand to half screen width.
Right Click menu with option to "Add Rule". Include "Export Rules" option.
When adding a Rule, all fields should be on one single Modal dialog. Make sure the dialog is deep enough to contain all controls.
The Rule dialog has a "Delete" option, if clicked it should offer "Are you sure?" dialog.
Include a button on the Rule Dialog to syntax check the statement. The button should be placed beside the statement text box, right hand side.
The rule panel should be a bit wider and may be more than 50% of the main form width.

Right - Data List. Title should be in bold "Registered Data". Include headers: Dataset Name, Data Item Name, Data Item Value. Double click row should open modal window and allow edit of name and value, or deletion of data item. Value is not JSON so do not add JSON comment to edit box for Value. Edit Data modal window needs to be deep enough to show all controls. If last data item is deleted, dataset should be deleted.
Right Click menu with option to "Add Data Item". Include "Export Data" option.
When adding a Data Item, all fields should be on one single Modal dialog. Make sure the dialog is deep enough to contain all controls.
The Data dialog has a "Delete" option, if clicked it should offer "Are you sure?" dialog

Ribbon to be 2 lines deep.
Top line: Name of loaded Session , Operators Dropdown. Right justofied for Rule application methods and Apply Rules button.
Ribbon - The Session name can be "Not loaded" at start up. Populated when a Session is created or loaded.
Ribbon - Operators should be drop down.
Ribbon - Apply Rules button. When clicked, rules for selected context should be applied to data in selected session.

Second line: 
Dropdown List of registered Context, include option for "(all contexts)", 
Dropdown list of registered Datasets, include option for "(all datasets)".
Make the dropdown list boxes sensible size, not full panel width.
The Context dropdown should line up with Registered Rules List panel
The Datasets dropdown should line up with Registered Data panel.

Activity and Results
Right click menu with option to clear activity and results data.
Include StatementApplied field in results when rules are applied.

# Code location
The solution should be saved to: "C:\Development\BSoft\RightRule_Developer"
Project files should be saved below that folder in "C:\Development\BSoft\RightRule_Developer\RightRule_Developer"
The RightRule library packages are available in: "C:\Development\BSoft\RightRule_Developer\RightRule_Developer\Packages\"
For RightRule library source code reference only - you can look at files in C:\Development\BSoft\RightRule\src
For RightRule Common component source code reference only - you can look in C:\Development\BSoft\RightRule\rightrule.system.common

