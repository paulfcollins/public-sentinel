# Solution for Azure Sentinel to ingest Fastly WAF Logs

<b>Background</b>
<p>
This solution was created on the back of a specific requirement during a Azure Sentinel Proof of Concept. Fastly WAF can be configured to export its log files into Azure Blob Storage but then these log files need to be processed and ingested into the Azure Log Analytics workspace, so that the data can be used in Kusto Query Lanaguage (KQL) queries and Azure Sentinel alerts.

The Visual Studio solution files are in the VS Solution folder.
</p>
