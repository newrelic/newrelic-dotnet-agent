package indexer

const DefaultTemplate = `
{{ define "Directory" }}
	<tr>
		<td><a href="/{{ .Prefix }}">{{ .Name }}/</a></td>
		<td>{{ .LastModified.Format "02-Jan-2006 15:04" }}</td>
		<td></td>
	</tr>
{{ end }}
{{ define "ParentDirectory" }}
	<tr>
		<td><a href="/{{ .Prefix }}">../</a></td>
		<td></td>
		<td></td>
	</tr>
{{ end }}
{{ define "File" }}
	<tr>
		<td><a href="/{{ .Key }}">{{ .Name }}</a></td>
		<td>{{ .LastModified.Format "02-Jan-2006 15:04" }}</td>
		<td>{{ .Size }}</td>
	</tr>
{{ end }}
{{ define "Title" }}Index of /{{ .Prefix }}{{ end }}
<!DOCTYPE HTML>
<html>
	<head>
		<meta charset="UTF-8">
		<title>{{ template "Title" . }}</title>
		<style>
			table {
				font-family: monospace;
			}
			th:first-child, td:first-child {
				min-width: 50vw;
			}
			td, th {
				padding-right: 5em;
				text-align: left;
			}
		</style>
	</head>
	<body>
		<h1>{{ template "Title" . }}</h1>
		<table>
			<thead>
				<tr>
					<th>Name</th>
					<th>Time</th>
					<th>Size</th>
				</tr>
			</thead>
			<tbody>
				{{ if .Parent }}
					{{ template "ParentDirectory" .Parent }}
				{{ end }}
				{{ range $dir := .Directories }}
					{{ template "Directory" $dir }}
				{{ end }}
				{{ range $file := .Files }}
					{{ template "File" $file }}
				{{ end }}
			</tbody>
		</table>
	</body>
</html>`
