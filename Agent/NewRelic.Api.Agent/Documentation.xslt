<?xml version="1.0" encoding="utf-8"?>
<!-- ================================================================================ -->
<!-- Amend, distribute, spindle and mutilate as desired, but don't remove this header -->
<!-- A simple XML Documentation to basic HTML transformation stylesheet -->
<!-- (c)2005 by Emma Burrows -->
<!-- ================================================================================ -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<!-- DOCUMENT TEMPLATE -->
	<!-- Format the whole document as a valid HTML document -->
	<xsl:template match="/">
		<HTML>
			<head>
				<title>New Relic .NET Agent API</title>
				<link rel="stylesheet" type="text/css" href="stylesheet.css"/>
			</head>
			<BODY>
				<xsl:apply-templates select="//assembly"/>
				<div class="copyright">
					<p>©2008-2019 New Relic, Inc. All rights reserved.</p>
				</div>
			</BODY>			
		</HTML>
	</xsl:template>

	<!-- ASSEMBLY TEMPLATE -->
	<!-- For each Assembly, display its name and then its member types -->
	<xsl:template match="assembly">
		<H1 class="assembly">
			<xsl:value-of select="name"/> Namespace
		</H1>
		<xsl:apply-templates select="//member[contains(@name,'T:')]"/>
	</xsl:template>

	<!-- TYPE TEMPLATE -->
	<!-- Loop through member types and display their properties and methods -->
	<xsl:template match="//member[contains(@name,'T:')]">

		<!-- Two variables to make code easier to read -->
		<!-- A variable for the name of this type -->
		<xsl:variable name="MemberName"
					   select="substring-after(@name, '.')"/>

		<!-- Get the type's fully qualified name without the T: prefix -->
		<xsl:variable name="FullMemberName"
					   select="substring-after(@name, ':')"/>

		<!-- Display the type's name and information -->
		<H2 class="class">
			<xsl:value-of select="$MemberName"/> Class
		</H2>
		<xsl:apply-templates/>

		<!-- If this type has public fields, display them -->
		<xsl:if test="//member[contains(@name,concat('F:',$FullMemberName))]">
			<H3>Fields</H3>

			<xsl:for-each select="//member[contains(@name,concat('F:',$FullMemberName))]">
				<H4>
					<xsl:value-of select="substring-after(@name, concat('F:',$FullMemberName,'.'))"/>
				</H4>
				<xsl:apply-templates/>
			</xsl:for-each>
		</xsl:if>

		<!-- If this type has properties, display them -->
		<xsl:if test="//member[contains(@name,concat('P:',$FullMemberName))]">
			<H3>Properties</H3>

			<xsl:for-each select="//member[contains(@name,concat('P:',$FullMemberName))]">
				<H4>
					<xsl:value-of select="substring-after(@name, concat('P:',$FullMemberName,'.'))"/>
				</H4>
				<xsl:apply-templates/>
			</xsl:for-each>
		</xsl:if>

		<!-- If this type has methods, display them -->
		<xsl:if test="//member[contains(@name,concat('M:',$FullMemberName))]">
			<H3 class="methods">Methods</H3>

			<table class="methods-table">
				<tr>
					<th>Name</th>
					<th>Description</th>
				</tr>
				<xsl:for-each select="//member[contains(@name,concat('M:',$FullMemberName))]">
					<xsl:if test="not(skip)">
						<tr>
							<td>
								<a href="#{generate-id(@name)}">
									<xsl:value-of select="substring-after(@name, concat('M:',$FullMemberName,'.'))"/>
								</a>
							</td>
							<td>
								<xsl:apply-templates select="summary"/>
							</td>
						</tr>
					</xsl:if>
				</xsl:for-each>
			</table>

			<H3 class="methods">Methods</H3>
			
			<xsl:for-each select="//member[contains(@name,concat('M:',$FullMemberName))]">
				<xsl:if test="not(skip)">
					<a name="{generate-id(@name)}"></a>
					<!-- If this is a constructor, display the type name 
				(instead of "#ctor"), or display the method name -->
					<H4>
						<xsl:choose>
							<xsl:when test="contains(@name, '#ctor')">
								Constructor:
								<xsl:value-of select="$MemberName"/>
								<xsl:value-of select="substring-after(@name, '#ctor')"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:value-of select="substring-after(@name, concat('M:',$FullMemberName,'.'))"/>
							</xsl:otherwise>
						</xsl:choose>
						Method
					</H4>

					<xsl:apply-templates select="summary"/>

					<!-- Display parameters if there are any -->
					<xsl:if test="count(param)!=0">
						<H5>Parameters</H5>
						<xsl:apply-templates select="param"/>
					</xsl:if>

					<!-- Display return value if there are any -->
					<xsl:if test="count(returns)!=0">
						<xsl:apply-templates select="returns"/>
					</xsl:if>

					<!-- Display exceptions if there are any -->
					<xsl:if test="count(exception)!=0">
						<H5>Exceptions</H5>
						<xsl:apply-templates select="exception"/>
					</xsl:if>

					<!-- Display examples if there are any -->
					<xsl:if test="count(example)!=0">
						<xsl:apply-templates select="example"/>
					</xsl:if>

					<hr/>
				</xsl:if>
			</xsl:for-each>

		</xsl:if>
	</xsl:template>

	<!-- OTHER TEMPLATES -->
	<!-- Templates for other tags -->
	<xsl:template match="c">
		<CODE>
			<xsl:apply-templates />
		</CODE>
	</xsl:template>

	<xsl:template match="code">
		<PRE>
			<xsl:apply-templates />
		</PRE>
	</xsl:template>

	<xsl:template match="example">
		<P>
			<STRONG>Example: </STRONG>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="exception">
		<P>
			<STRONG>
				<xsl:value-of select="substring-after(@cref,'T:')"/>:
			</STRONG>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="include">
		<A HREF="{@file}">External file</A>
	</xsl:template>

	<xsl:template match="para">
		<P>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="param">
		<dl>
			<dt>
				<span class="parameter">
					<xsl:value-of select="@name"/>
				</span>
			</dt>
			<dd>
				<span>
					<xsl:apply-templates />
				</span>
			</dd>
		</dl>
	</xsl:template>

	<xsl:template match="paramref">
		<EM>
			<xsl:value-of select="@name" />
		</EM>
	</xsl:template>

	<xsl:template match="permission">
		<P>
			<STRONG>Permission: </STRONG>
			<EM>
				<xsl:value-of select="@cref" />
			</EM>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="remarks">
		<P>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="returns">
		<P>
			<STRONG>Return Value: </STRONG>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="see">
		<EM>
			See: <xsl:value-of select="@cref" />
		</EM>
	</xsl:template>

	<xsl:template match="seealso">
		<EM>
			See also: <xsl:value-of select="@cref" />
		</EM>
	</xsl:template>

	<xsl:template match="summary">
		<P>
			<xsl:apply-templates />
		</P>
	</xsl:template>

	<xsl:template match="list">
		<xsl:choose>
			<xsl:when test="@type='bullet'">
				<UL>
					<xsl:for-each select="listheader">
						<LI>
							<strong>
								<xsl:value-of select="term"/>:
							</strong>
							<xsl:value-of select="definition"/>
						</LI>
					</xsl:for-each>
					<xsl:for-each select="list">
						<LI>
							<strong>
								<xsl:value-of select="term"/>:
							</strong>
							<xsl:value-of select="definition"/>
						</LI>
					</xsl:for-each>
				</UL>
			</xsl:when>
			<xsl:when test="@type='number'">
				<OL>
					<xsl:for-each select="listheader">
						<LI>
							<strong>
								<xsl:value-of select="term"/>:
							</strong>
							<xsl:value-of select="definition"/>
						</LI>
					</xsl:for-each>
					<xsl:for-each select="list">
						<LI>
							<strong>
								<xsl:value-of select="term"/>:
							</strong>
							<xsl:value-of select="definition"/>
						</LI>
					</xsl:for-each>
				</OL>
			</xsl:when>
			<xsl:when test="@type='table'">
				<TABLE>
					<xsl:for-each select="listheader">
						<TH>
							<TD>
								<xsl:value-of select="term"/>
							</TD>
							<TD>
								<xsl:value-of select="definition"/>
							</TD>
						</TH>
					</xsl:for-each>
					<xsl:for-each select="list">
						<TR>
							<TD>
								<strong>
									<xsl:value-of select="term"/>:
								</strong>
							</TD>
							<TD>
								<xsl:value-of select="definition"/>
							</TD>
						</TR>
					</xsl:for-each>
				</TABLE>
			</xsl:when>
		</xsl:choose>
	</xsl:template>

</xsl:stylesheet>
