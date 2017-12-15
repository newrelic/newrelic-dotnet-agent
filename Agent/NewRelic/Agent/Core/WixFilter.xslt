<xsl:stylesheet
		version="1.0"
		xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
		xmlns:msxsl="urn:schemas-microsoft-com:xslt"
		exclude-result-prefixes="msxsl"
		xmlns:wix="http://schemas.microsoft.com/wix/2006/wi"
		xmlns:my="my">
	<xsl:output method="xml" indent="yes"/>
	<xsl:strip-space elements="*"/>
	<xsl:template match="@*|*">
		<xsl:copy>
			<xsl:apply-templates select="@*" />
			<xsl:apply-templates select="*" />
		</xsl:copy>
	</xsl:template>

	<xsl:key name="dll-only" match="wix:Component[not(contains(wix:File/@Source, '.dll'))]" use="@Id"/>
	<xsl:template match="wix:Component[key('dll-only', @Id)]"/>
	<xsl:template match="wix:ComponentRef[key('dll-only', @Id)]"/>

	<xsl:key name="remove-subdirectories" match="wix:Directory/wix:Component" use="@Id"/>
	<xsl:template match="wix:ComponentRef[key('remove-subdirectories', @Id)]"/>
	<xsl:template match="wix:Directory"/>
</xsl:stylesheet>
