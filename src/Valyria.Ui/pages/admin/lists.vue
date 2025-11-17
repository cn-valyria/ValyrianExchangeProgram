<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Lists</h1>
        <h2 class="subtitle">View and manage the lists of nations that should be sending or receiving aid</h2>
      </div>
    </div>
    <div class="container" style="position: relative">
      <ProgressSpinner v-if="isLoading" style="width: 50px; height: 50px; position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" />
      <div class="section">
        <TabView v-if="!isLoading">
          <TabPanel v-for="list in lists" :key="list.id" :header="list.name">
            <div class="content">
              <h3>Message Template</h3>
              <div class="field">
                <label class="label">Carbon Copy Rulers</label>
                <Textarea readonly :value="getRulerList(list)" :autoResize="true" rows="5" />
              </div>
              <div class="field">
                <label class="label">Subject</label>
                <InputText readonly :value="list.subject.replace('{{date}}', todayFormatted)" class="w-full" />
              </div>
              <div class="field">
                <label class="label">Message</label>
                <Textarea readonly :value="list.message" :autoResize="true" rows="10" />
              </div>
              <h3>Data</h3>
              <DataTable :value="list.recipients" paginator :rows="25">
                <Column field="nation" header="Nation">
                  <template #body="slotProps">
                    {{ slotProps.data.nation.rulerName }} of {{ slotProps.data.nation.nationName }}
                  </template>
                </Column>
                <Column field="slotsFree" header="Slots Free">
                  <template #body="slotProps">
                    {{ slotProps.data.slotsFree }}
                  </template>
                </Column>
                <Column v-if="list.name.includes('Incoming')" field="credit" header="Credit">
                  <template #body="slotProps">
                    {{ slotProps.data.credit }}
                  </template>
                </Column>
                <Column v-else field="debt" header="Debt">
                  <template #body="slotProps">
                    {{ slotProps.data.debt }}
                  </template>
                </Column>
                <Column field="recentActivity" header="Activity">
                  <template #body="slotProps">
                    {{ getActivityDescription(slotProps.data.recentActivity) }}
                  </template>
                </Column>
                <Column field="discord" header="Discord Tag">
                  <template #body="slotProps">
                    {{ slotProps.data.discord }}
                  </template>
                </Column>
                <Column field="id" header="Message">
                  <template #body="slotProps">
                    <a :href="getNationMessageLink(slotProps.data.nation)" target="_blank">Click to Send</a>
                  </template>
                </Column>
              </DataTable>
            </div>
          </TabPanel>
        </TabView>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAdminListsStore, type AidList } from '~/stores/adminLists'
import { useAuthStore } from '~/stores/auth'
import { recentActivityValues } from '~/infrastructure/dataLists'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

const router = useRouter()
const toast = useToast()

const adminListsStore = useAdminListsStore()
const authStore = useAuthStore()

const { lists } = storeToRefs(adminListsStore)
const { loggedInUser } = storeToRefs(authStore)

const isLoading = ref<boolean>(true)

// Computed property for today's formatted date
const todayFormatted = computed<string>(() => {
  const now = new Date()
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  const month = months[now.getMonth()]
  const day = now.getDate().toString().padStart(2, '0')
  return `${month} ${day}`
})

// Helper function to get ruler list as string
const getRulerList = (aidList: AidList): string => {
  return aidList.recipients.map(recipient => recipient.nation.rulerName).join('\r\n')
}

// Helper function to get nation message link
const getNationMessageLink = (nation: NationSimplified): string => {
  return `https://www.cybernations.net/send_message.asp?Nation_ID=${nation.nationId}`
}

// Helper function to get activity description
const getActivityDescription = (activityId: number): string => {
  const activity = recentActivityValues.find(v => v.id === activityId)
  return activity ? activity.description : 'Unknown'
}

// Check admin access and load lists on mount
onMounted(async () => {
  // Check if user has admin or admin-lists role
  const hasAccess = loggedInUser.value?.roles?.some((role: any) =>
    role.name === 'Admin' || role.name === 'Admin - Lists'
  )

  if (!hasAccess) {
    console.warn('Tried to access an admin page without the admin role.')
    toast.add({
      color: 'error',
      title: 'Access Denied',
      description: 'You do not have permission to access this page.',
      duration: 5000
    })
    await router.push('/')
    return
  }

  try {
    await adminListsStore.getAllAidLists()
    console.log('Lists loaded:', lists.value)
  } catch (error) {
    console.error('Failed to load lists:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load aid lists. Please try again.',
      duration: 5000
    })
  } finally {
    isLoading.value = false
  }
})
</script>
