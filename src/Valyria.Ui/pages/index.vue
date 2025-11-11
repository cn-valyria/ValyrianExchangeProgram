<template>
  <div>
    <section class="hero is-medium is-dark">
      <div class="hero-body has-text-centered" style="padding-bottom: 4.5rem">
        <p class="title">Valyrian Exchange Program</p>
        <p class="subtitle">
          Fully managed CN tech dealing
        </p>
      </div>
    </section>
    <div class="container has-text-centered">
      <img id="dragons-illustration" src="~/assets/img/dragons-illustration.png" />
    </div>
    <div class="container">
      <div class="section">
        <div class="is-size-3 has-text-weight-semibold has-text-centered mb-6">
          An account that always pays its debts
        </div>
        <div class="columns is-variable is-8">
          <div class="column is-three-fifths">
            <div class="box" style="padding: 0">
              <figure class="image">
                <img src="~/assets/img/account-example.png" />
              </figure>
            </div>
          </div>
          <div class="column">
            <div class="content">
              <h3>Manage your own balance instead of individual deals</h3>
              <p>
                If you've played CN before, you're probably familiar with the concept of a "tech deal": a buyer sends 
                cash to a seller in exchange for technology. They can be great for new players and ancient nations 
                alike, but they typically require a lot of research, maintenance, and tracking in order to maintain 
                healthy deals long-term.
              </p>
              <p>
                The Valyrian Exchange Program is a new paradigm in tech dealing. Instead of managing individual deals, 
                you are given a simple ledger - known as your balance - that tracks how much aid you've sent, how much 
                you're owed, and how much you're due to send. VEP enrolls well over one hundred nations, with an even 
                split of buyers and sellers, so you're guaranteed a consistent source of incoming aid and outgoing 
                targets. Never worry about 1x1 deals again; get signed up in VEP today!
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <div class="is-size-3 has-text-weight-semibold has-text-centered mb-6">
          Features that you can't get in other programs
        </div>
        <div class="columns">
          <div class="column is-4 is-offset-1">
            <div class="columns">
              <div class="column is-narrow">
                <ion-icon name="diamond-outline" size="large"></ion-icon>
              </div>
              <div class="column">
                <div class="content">
                  <h5>Lovely Website</h5>
                  <p>
                    Let's be honest: CN isn't known for being a very attractive app. It doesn't have to be that 
                    way for a wonderful tool such as VEP, though! And VEP isn't just pretty; it is thoughtfully-
                    crafted to be simple and easy to understand.
                  </p>
                </div>
              </div>
            </div>
          </div>
          <div class="column is-4 is-offset-2">
            <div class="columns">
              <div class="column is-narrow">
                <ion-icon name="speedometer-outline" size="large"></ion-icon>
              </div>
              <div class="column">
                <div class="content">
                  <h5>Constant Aid</h5>
                  <p>
                    In VEP, it's assumed that if you have an open aid slot, then you'd like to either receive aid or 
                    send more out. We want to make sure that you're using your aid slots to their max capacity!
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="columns mt-6">
          <div class="column is-4 is-offset-1">
            <div class="columns">
              <div class="column is-narrow">
                <ion-icon name="cash-outline" size="large"></ion-icon>
              </div>
              <div class="column">
                <div class="content">
                  <h5>Fair Rates</h5>
                  <p>
                    9/100: $9 million in cash for 100 technology levels. That's been VEP's rate since it was founded, 
                    and it's the fixed rate for VEP's future.
                  </p>
                </div>
              </div>
            </div>
          </div>
          <div class="column is-4 is-offset-2">
            <div class="columns">
              <div class="column is-narrow">
                <ion-icon name="cog-outline" size="large"></ion-icon>
              </div>
              <div class="column">
                <div class="content">
                  <h5>Automated Transactions</h5>
                  <p>
                    All aid is imported and tallied against your balance every morning, in CN time, so you'll never 
                    need to track aid screens again.
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
        <p class="has-text-centered is-size-5 mt-6">...and so much more!</p>
      </div>
    </div>
    <section class="hero is-primary">
      <div class="hero-body has-text-centered">
        <p class="title">Start managing your account now!</p>
        <p class="subtitle">
          If you already have an account, dig into our additional resources to learn more about the nuts and bolts of 
          how VEP works
        </p>
        <a class="button is-white">How VEP Works</a>
      </div>
    </section>
    <footer class="footer">
      <div class="content has-text-centered">
        Built with &#10084;&#65039; by lilweirdward and dev0win. Founded by Lord Draculea.
      </div>
    </footer>
  </div>
</template>

<style>
img#dragons-illustration {
  width: 375px;
  margin-top: -2rem;
}
</style>

<script>
export default {
  name: 'IndexPage',
  auth: false,
  created() {
    if (this.$route.query.auth && this.$route.query.auth === "failed") {
      this.$toast.error("You can't access that page until you're logged in. Please log into your account, or contact VEP management to create one for you.");
    }
  }
}
</script>
